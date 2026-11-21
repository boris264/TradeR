using Backtester.Engine.Interfaces;
using Backtester.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Excel;
using TradeR.Services.Market.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.Services.Models.Signals;
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
            IServiceProvider serviceProvider)
        {
            this.engineConfiguration = engineConfiguration;
            this.strategy = strategy;

            logger = serviceProvider.GetRequiredService<ILogger<HistoricalDataEngine>>();
            marketDataLoader = serviceProvider.GetRequiredService<IMarketDataLoader>();
            pnLCalculator = new PnLCalculator(fee: 0.01);

            account = new Account()
            {
                Balance = engineConfiguration.InitialBalance,
                RiskSize = engineConfiguration.RiskSize
            };
        }

        public async Task<Result> StartAsync(CancellationToken cancellationToken)
        {
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

                return Result.Failure(loadMarketDataResult.Error);
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
                        HandleEntrySignal(entrySignal!, candle);
                    }
                    else
                    {
                        var exitSignal = currentSignal as ExitSignal;
                        HandleExitSignal(exitSignal!, candle);
                    }
                }

                currentSignal = strategy.Next(candle);
                currentCandle = candle;
            }

            if (openPosition != null)
            {
                // Manually close the position
                var closeOrder = new Order()
                {
                    Type = openPosition.Direction == PositionDirection.Long
                           ? OrderType.Buy
                           : OrderType.Sell,
                    Amount = openPosition.OpenOrder.Amount,
                    Price = currentCandle.Close
                };
                openPosition.Close(closeOrder, currentCandle!.Timestamp, "End of backtest");

                var trade = NewTradeRow();
                SaveTradeToExcel(trade);
            }

            return Result.Success();
        }

        private void HandleEntrySignal(EntrySignal signal, OhlcvtCandle candle)
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

        private void HandleExitSignal(ExitSignal signal, OhlcvtCandle candle)
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
                Price = candle.Open
            };
            openPosition.Close(closeOrder, candle.Timestamp, signal.Reason);

            var trade = NewTradeRow();
            SaveTradeToExcel(trade);
        }

        private void SaveTradeToExcel(TradeRow trade)
        {
            // Empty for now
        }

        private TradeRow NewTradeRow()
        {
            var pnl = pnLCalculator.Calculate(
                openPosition!.OpenOrder.Price,
                openPosition.OpenOrder.Amount,
                openPosition.CloseOrder!.Price,
                openPosition.CloseOrder.Amount,
                openPosition.Direction);

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
                OpenTime = DateTimeOffset
                    .FromUnixTimeSeconds(openPosition.OpenTimestamp)
                    .ToUniversalTime()
                    .Date,
                CloseTime = DateTimeOffset
                    .FromUnixTimeSeconds(openPosition.CloseTimestamp)
                    .ToUniversalTime()
                    .Date,
                EntryReason = openPosition.EntryReason,
                ExitReason = openPosition.ExitReason ?? "End of backtest"
            };
        }
    }
}
