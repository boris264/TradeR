using Backtester.Configurations;
using Backtester.Engine.Interfaces;
using Backtester.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly double fee;

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
            fee = serviceScope.ServiceProvider.GetRequiredService<IOptions<BacktesterConfiguration>>().Value.Fee;
            pnLCalculator = new PnLCalculator(fee);

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
                    if (signalType == SignalType.Entry && openPosition == null)
                    {
                        var entrySignal = (EntrySignal)currentSignal;
                        if (CanOpenPosition(entrySignal, candle))
                        {
                            OpenPosition(entrySignal, candle);

                            strategy.ChangeState(GetStrategyStateFromPosition(openPosition!.Direction));

                            logger.LogInformation(
                                "Opened {Direction} position using a signal from the {Strategy} strategy, reason {Reason}",
                                openPosition.Direction,
                                strategy.Name,
                                entrySignal.Reason);
                        }
                    }
                    else if (signalType == SignalType.Exit && openPosition != null)
                    {
                        var exitSignal = currentSignal as ExitSignal;
                        logger.LogInformation(
                                "Closed {Direction} position using a signal from the {Strategy} strategy, reason {Reason}",
                                openPosition.Direction,
                                strategy.Name,
                                exitSignal!.Reason);

                        ClosePosition(candle.Open, candle.Timestamp, exitSignal!.Reason, trades);
                        strategy.ChangeState(StrategyState.Idle);
                    }
                }

                if (openPosition != null)
                {
                    bool takeProfitCompareFunc() => openPosition.TakeProfit != null &&
                              (openPosition.Direction == PositionDirection.Long
                                   ? (candle.Open >= openPosition.TakeProfit.Price || candle.High >= openPosition.TakeProfit.Price)
                                   : candle.Open <= openPosition.TakeProfit.Price || candle.Low <= openPosition.TakeProfit.Price);
                    bool stopLossCompareFunc() => openPosition.StopLoss != null &&
                              (openPosition.Direction == PositionDirection.Long
                                   ? (candle.Open <= openPosition.StopLoss.Price || candle.Low <= openPosition.StopLoss.Price)
                                   : (candle.Open >= openPosition.StopLoss.Price || candle.High >= openPosition.StopLoss.Price));

                    if (stopLossCompareFunc())
                    {
                        logger.LogInformation(
                                "Closed {Direction} position based on the stop loss price from the {Strategy} strategy",
                                openPosition.Direction,
                                strategy.Name);

                        ClosePosition(openPosition.StopLoss!.Price, candle.Timestamp, "Stop Loss", trades);
                        strategy.ChangeState(StrategyState.Idle);
                    }
                    else if (takeProfitCompareFunc())
                    {
                        logger.LogInformation(
                                "Closed {Direction} position based on the take profit price from the {Strategy} strategy",
                                openPosition.Direction,
                                strategy.Name);

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

        private bool CanOpenPosition(EntrySignal signal, OhlcvtCandle candle)
        {
            if (account.Balance <= 0)
            {
                logger.LogInformation(
                    "Entry at {Timestamp} skipped: the account balance is {Balance}",
                    candle.Timestamp,
                    account.Balance);

                return false;
            }

            var fillIsPastStop = signal.Direction == PositionDirection.Long
                                     ? candle.Open <= signal.StopLossPrice
                                     : candle.Open >= signal.StopLossPrice;
            if (fillIsPastStop)
            {
                logger.LogInformation(
                    "Entry at {Timestamp} skipped: the fill price {Price} is already past the stop loss {StopLoss}",
                    candle.Timestamp,
                    candle.Open,
                    signal.StopLossPrice);

                return false;
            }

            return true;
        }

        private void OpenPosition(EntrySignal signal, OhlcvtCandle candle)
        {
            var amountToBuyBasedOnRiskSize = Math.Abs(account.RiskAmount / (candle.Open - signal.StopLossPrice));
            var maxAmountAffordable = account.Balance / (candle.Open * (1 + fee));
            var amountToBuy = Math.Min(amountToBuyBasedOnRiskSize, maxAmountAffordable);
            var mainOrder = new Order()
            {
                Type = signal.Direction == PositionDirection.Long
                           ? OrderType.Buy
                           : OrderType.Sell,
                Amount = amountToBuy,
                Price = candle.Open
            };
            openPosition = new Position(
                signal.Direction,
                mainOrder,
                candle.Timestamp,
                signal.Reason);

            if (signal.TakeProfitPrice != null)
            {
                openPosition.PlaceTakeProfit(amountToBuy, signal.TakeProfitPrice.Value);
            }

            openPosition.PlaceStopLoss(amountToBuy, signal.StopLossPrice);
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
            var openTime = DateTimeOffset
                .FromUnixTimeSeconds(openPosition.OpenTimestamp)
                .ToUniversalTime()
                .DateTime;
            var closeTime = DateTimeOffset
                .FromUnixTimeSeconds(openPosition.CloseTimestamp)
                .ToUniversalTime()
                .DateTime;

            openTime = DateTime.SpecifyKind(openTime, DateTimeKind.Utc);
            closeTime = DateTime.SpecifyKind(closeTime, DateTimeKind.Utc);

            account.Balance += pnl;

            return new TradeRow()
            {
                AssetPair = engineConfiguration.AssetPair,
                Interval = engineConfiguration.Interval,
                Strategy = strategy.Name,
                Position = openPosition.Direction,
                CoinAmount = openPosition.OpenOrder.Amount,
                BoughtAmount = openPosition.OpenOrder.Amount * openPosition.OpenOrder.Price,
                OpenPrice = openPosition.OpenOrder.Price,
                ClosePrice = openPosition.CloseOrder.Price,
                PnL = pnl,
                Fees = fees,
                OpenTime = openTime,
                CloseTime = closeTime,
                EntryReason = openPosition.EntryReason,
                ExitReason = openPosition.ExitReason ?? string.Empty,
                AccountSize = engineConfiguration.InitialBalance,
            };
        }

        private StrategyState GetStrategyStateFromPosition(PositionDirection positionDirection)
            => positionDirection switch
            {
                PositionDirection.Long => StrategyState.LongPositionOpened,
                PositionDirection.Short => StrategyState.ShortPositionOpened,
                _ => throw new ArgumentException("Invalid value for positionDirection!", nameof(positionDirection)),
            };
    }
}
