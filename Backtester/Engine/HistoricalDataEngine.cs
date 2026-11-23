using Backtester.Engine.Interfaces;
using Backtester.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Excel;
using TradeR.Services.Market.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.Services.Models.Signals;
using TradeR.Services.Strategies;
using TradeR.Services.Strategies.Interfaces;
using TradeR.SharedKernel.Common;
using TradeR.SharedKernel.Enums;
using TradeR.SharedKernel.Results;

namespace Backtester.Engine
{
    internal class HistoricalDataEngine : IEngine
    {
        private readonly EngineConfiguration engineConfiguration;
        private readonly Account account;
        private Position? openPosition;

        private readonly IMarketDataLoader marketDataLoader;
        private readonly IStrategy strategy;
        private readonly ILogger<HistoricalDataEngine> logger;
        private readonly PnLCalculator pnLCalculator;

        public string AssetPair => engineConfiguration.AssetPair;

        public HistoricalDataEngine(
            EngineConfiguration engineConfiguration,
            IStrategy strategy,
            IServiceScope serviceScope)
        {
            this.engineConfiguration = engineConfiguration;
            this.strategy = strategy;

            logger = serviceScope.ServiceProvider.GetRequiredService<ILogger<HistoricalDataEngine>>();
            marketDataLoader = serviceScope.ServiceProvider.GetRequiredService<IMarketDataLoader>();
            pnLCalculator = new PnLCalculator(fee: 0.01);

            account = new Account()
            {
                Balance = engineConfiguration.InitialBalance,
                RiskSize = engineConfiguration.RiskSize
            };
        }

        public async Task<Result<IEnumerable<TradeRow>>> StartAsync(CancellationToken cancellationToken)
        {
            var trades = new List<TradeRow>();

            var marketDataLoadRequest = new MarketDataLoadRequest()
            {
                AssetPair = engineConfiguration.AssetPair,
                From = engineConfiguration.From,
                To = engineConfiguration.To,
                Interval = engineConfiguration.Interval,
                CandlesToPreload = strategy.CandlesToPreload
            };
            var loadMarketDataResult = await marketDataLoader.Load(marketDataLoadRequest, cancellationToken);

            if (loadMarketDataResult.IsFailure)
            {
                logger.LogError(
                    "Failed to load market data for asset pair {AssetPair}, from {From}, to {To}, interval {Interval}, error {Error}",
                    engineConfiguration.AssetPair,
                    engineConfiguration.From,
                    engineConfiguration.To,
                    engineConfiguration.Interval,
                    loadMarketDataResult.Error);

                return Result.Failure<IEnumerable<TradeRow>>(loadMarketDataResult.Error);
            }

            Signal? currentSignal = null;
            OhlcvtCandle currentCandle = null!;
            var fromInUnixTime = new DateTimeOffset(engineConfiguration.From).ToUnixTimeSeconds();
            var candlesStream = loadMarketDataResult.Value;
            await foreach (var candle in candlesStream)
            {
                // We know that candles are preloaded by the MarketDataLoader
                // so we just check the candle's timestamp if it's less than the From timestamp
                if (candle.Timestamp < fromInUnixTime)
                {
                    strategy.Next(candle);
                    continue;
                }

                // From this point, we start the actual backtest
                // The current candle is the candle after we received the signal
                if (currentSignal != null)
                {
                    var signalType = currentSignal.Type;
                    if (signalType == SignalType.Entry)
                    {
                        var entrySignal = currentSignal as EntrySignal;
                        OpenPosition(entrySignal!, candle);

                        strategy.ChangeState(StrategyState.PositionOpened);
                    }
                    else
                    {
                        var exitSignal = currentSignal as ExitSignal;
                        ClosePosition(candle.Close, candle.Timestamp, exitSignal!.Reason, trades);

                        strategy.ChangeState(StrategyState.Idle);
                    }
                }

                if (openPosition != null)
                {
                    bool takeProfitCompareFunc() => openPosition.TakeProfit != null &&
                              (openPosition.Direction == PositionDirection.Long
                                   ? candle.Open >= openPosition.TakeProfit.Price
                                   : candle.Open <= openPosition.TakeProfit.Price);
                    bool stopLossCompareFunc() => openPosition.StopLoss != null &&
                              (openPosition.Direction == PositionDirection.Long
                                   ? candle.Open <= openPosition.StopLoss.Price
                                   : candle.Open >= openPosition.StopLoss.Price);

                    if (stopLossCompareFunc())
                    {
                        ClosePosition(openPosition.StopLoss!.Price, candle.Timestamp, "Stop Loss", trades);

                        strategy.ChangeState(StrategyState.Idle);
                    }
                    else if (takeProfitCompareFunc())
                    {
                        ClosePosition(openPosition.TakeProfit!.Price, candle.Timestamp, "Take Profit", trades);

                        strategy.ChangeState(StrategyState.Idle);
                    }
                }

                currentSignal = strategy.Next(candle);
                currentCandle = candle;
            }

            if (openPosition != null)
            {
                ClosePosition(currentCandle.Close, currentCandle.Timestamp, "End of backtest", trades);
            }

            return Result.Success(trades.AsEnumerable());
        }

        private void OpenPosition(EntrySignal signal, OhlcvtCandle candle)
        {
            var amountToBuyBasedOnRiskSize = Math.Abs(account.RiskAmount / (candle.Open - signal.StopLossPrice));
            var mainOrder = new Order()
            {
                Type = signal.Direction == PositionDirection.Long
                           ? OrderType.Buy
                           : OrderType.Sell,
                Amount = amountToBuyBasedOnRiskSize,
                Price = candle.Open
            };
            openPosition = new Position(
                signal.Direction,
                mainOrder,
                candle.Timestamp,
                signal.Reason);

            if (signal.TakeProfitPrice != null)
            {
                openPosition.PlaceTakeProfit(amountToBuyBasedOnRiskSize, signal.TakeProfitPrice.Value);
            }

            openPosition.PlaceStopLoss(amountToBuyBasedOnRiskSize, signal.StopLossPrice);
        }

        private void ClosePosition(double price, long timestamp, string reason, List<TradeRow> trades)
        {
            if (openPosition == null)
            {
                return;
            }

            var closeOrder = new Order()
            {
                Type = openPosition.Direction == PositionDirection.Long
                           ? OrderType.Sell
                           : OrderType.Buy,
                Amount = openPosition.OpenOrder.Amount,
                Price = price
            };
            openPosition.Close(closeOrder, timestamp, reason);

            var trade = NewTradeRow();
            trades.Add(trade);

            openPosition = null;
        }

        private TradeRow NewTradeRow()
        {
            var pnl = pnLCalculator.Calculate(
                openPosition!.OpenOrder.Price,
                openPosition.OpenOrder.Amount,
                openPosition.CloseOrder!.Price,
                openPosition.CloseOrder.Amount,
                openPosition.Direction);
            var fees = pnLCalculator.CalculateFees(
                openPosition!.OpenOrder.Price,
                openPosition.OpenOrder.Amount,
                openPosition.CloseOrder!.Price,
                openPosition.CloseOrder.Amount);

            return new TradeRow()
            {
                AssetPair = engineConfiguration.AssetPair,
                Interval = engineConfiguration.Interval,
                Strategy = strategy.Name,
                Position = openPosition.Direction,
                CoinAmount = openPosition.OpenOrder.Amount,
                BoughtAmount = openPosition.OpenOrder.Amount * openPosition.OpenOrder.Price,
                BuyPrice = openPosition.OpenOrder.Price,
                SellPrice = openPosition.CloseOrder.Price,
                PnL = pnl,
                Fees = fees,
                OpenTime = DateTimeOffset
                    .FromUnixTimeSeconds(openPosition.OpenTimestamp)
                    .ToUniversalTime()
                    .DateTime,
                CloseTime = DateTimeOffset
                    .FromUnixTimeSeconds(openPosition.CloseTimestamp)
                    .ToUniversalTime()
                    .DateTime,
                EntryReason = openPosition.EntryReason,
                ExitReason = openPosition.ExitReason ?? string.Empty,
                AccountSize = account.Balance,
            };
        }
    }
}
