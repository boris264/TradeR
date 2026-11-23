using Backtester.Configurations;
using Backtester.Engine;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
using TradeR.SharedKernel.Enums;
using TradeR.SharedKernel.Results;
using TradeR.Tests.Backtester.Engine.Fakes;

namespace TradeR.Tests.Backtester.Engine
{
    [TestFixture]
    public class HistoricalDataEngineTests
    {
        private const double Tolerance = 1e-6;
        private const string AssetPair = "XBTUSD";
        private const string StrategyName = "Fake strategy";
        private const string EntryReason = "Test entry";
        private const string ExitReason = "Test exit";
        private const string StopLossReason = "Stop Loss";
        private const string TakeProfitReason = "Take Profit";
        private const string EndOfBacktestReason = "End of backtest";
        private const MarketInterval Interval = MarketInterval.FifteenMinutes;
        private const long IntervalSeconds = 15 * 60;
        private const double InitialBalance = 10000.0;
        private const double RiskSize = 0.01;
        private const double Fee = 0.004;

        private static readonly DateTime From = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime To = From.AddDays(1);
        private static readonly long FromUnix = new DateTimeOffset(From).ToUnixTimeSeconds();

        [Test]
        public async Task StartAsync_Always_RequestsCandlesForConfiguredRun()
        {
            FakeStrategy strategy = new FakeStrategy(StrategyName, candlesToPreload: 7);
            FakeMarketDataLoader loader = new FakeMarketDataLoader(new List<OhlcvtCandle>());

            await RunAsync(strategy, loader);

            MarketDataLoadRequest? request = loader.ReceivedRequest;
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.AssetPair, Is.EqualTo(AssetPair));
            Assert.That(request.From, Is.EqualTo(From));
            Assert.That(request.To, Is.EqualTo(To));
            Assert.That(request.Interval, Is.EqualTo(Interval));
            Assert.That(request.CandlesToPreload, Is.EqualTo(7));
        }

        [Test]
        public async Task StartAsync_LoaderFails_ReturnsSameErrorAndFeedsNoCandles()
        {
            Error error = new Error("Test.LoadFailed", "The candles could not be loaded");
            FakeStrategy strategy = new FakeStrategy(StrategyName);
            FakeMarketDataLoader loader = new FakeMarketDataLoader(error);

            Result<IEnumerable<TradeRow>> result = await RunAsync(strategy, loader);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(strategy.ReceivedCandles, Is.Empty);
        }

        [Test]
        public async Task StartAsync_NoCandles_ReturnsNoTrades()
        {
            FakeStrategy strategy = new FakeStrategy(StrategyName);

            List<TradeRow> rows = await RunTradesAsync(strategy, new List<OhlcvtCandle>());

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public async Task StartAsync_WithPreloadCandles_FeedsEveryCandleToStrategyOnceInOrder()
        {
            FakeStrategy strategy = new FakeStrategy(StrategyName, candlesToPreload: 2);
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(-2, 100), Flat(-1, 101), Flat(0, 102), Flat(1, 103), Flat(2, 104)
            };

            await RunTradesAsync(strategy, candles);

            Assert.That(
                strategy.ReceivedCandles.Select(x => x.Timestamp),
                Is.EqualTo(candles.Select(x => x.Timestamp)));
        }

        [Test]
        public async Task StartAsync_EntrySignalOnPreloadCandle_TakesNoTrade()
        {
            FakeStrategy strategy = new FakeStrategy(StrategyName, candlesToPreload: 3)
                .SignalOn(T(-3), Entry(PositionDirection.Long, stop: 50));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(-3, 100), Flat(-2, 100), Flat(-1, 100), Flat(0, 100), Flat(1, 100), Flat(2, 100)
            };

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public async Task StartAsync_EntrySignal_FillsAtNextCandleOpen()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 105, 106, 107, 104),
                Bar(2, 106, 107, 108, 105)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.OpenPrice, Is.EqualTo(105).Within(Tolerance));
            Assert.That(row.OpenTime, Is.EqualTo(ToUtc(T(1))));
            Assert.That(row.OpenTime.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public async Task StartAsync_ExitSignal_FillsAtNextCandleOpen()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90))
                .SignalOn(T(2), Exit());
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 101, 102, 99),
                Bar(2, 103, 104, 105, 102),
                Bar(3, 108, 107, 109, 106),
                Bar(4, 107, 107.5, 108, 106.5)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ClosePrice, Is.EqualTo(108).Within(Tolerance));
            Assert.That(row.CloseTime, Is.EqualTo(ToUtc(T(3))));
            Assert.That(row.CloseTime.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(row.ExitReason, Is.EqualTo(ExitReason));
        }

        [Test]
        public async Task StartAsync_EntrySignalOnLastCandle_TakesNoTrade()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(2), Entry(PositionDirection.Long, stop: 90));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 100), Flat(1, 100), Flat(2, 100)
            };

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public async Task StartAsync_ExitSignalOnLastCandle_ClosesAsEndOfBacktest()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90))
                .SignalOn(T(2), Exit());
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 101, 102, 99),
                Bar(2, 103, 104, 105, 102)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ClosePrice, Is.EqualTo(104).Within(Tolerance));
            Assert.That(row.ExitReason, Is.EqualTo(EndOfBacktestReason));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_StopLossTouched_ClosesAtStopPrice(PositionDirection direction)
        {
            double s = Sign(direction);
            double stop = 100 - 10 * s;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop, takeProfit: 100 + 20 * s));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 + 1 * s, 100 + 2 * s, 100 - 2 * s),
                Bar(2, 100 - 1 * s, 100 - 2 * s, stop, 100 + 1 * s),
                Flat(3, 100)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ClosePrice, Is.EqualTo(stop).Within(Tolerance));
            Assert.That(row.ExitReason, Is.EqualTo(StopLossReason));
            Assert.That(row.CloseTime, Is.EqualTo(ToUtc(T(2))));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_TakeProfitTouched_ClosesAtTakeProfitPrice(PositionDirection direction)
        {
            double s = Sign(direction);
            double takeProfit = 100 + 20 * s;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop: 100 - 10 * s, takeProfit));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 + 1 * s, 100 + 2 * s, 100 - 2 * s),
                Bar(2, 100 + 1 * s, 100 + 2 * s, takeProfit, 100 - 1 * s),
                Flat(3, 100)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ClosePrice, Is.EqualTo(takeProfit).Within(Tolerance));
            Assert.That(row.ExitReason, Is.EqualTo(TakeProfitReason));
            Assert.That(row.CloseTime, Is.EqualTo(ToUtc(T(2))));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_LevelsNotReached_PositionStaysOpenUntilEnd(PositionDirection direction)
        {
            double s = Sign(direction);
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop: 100 - 10 * s, takeProfit: 100 + 20 * s));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 + 1 * s, 100 + 2 * s, 100 - 2 * s),
                Bar(2, 100 - 1 * s, 100 - 2 * s, 100 - 9.99 * s, 100 + 19.99 * s),
                Bar(3, 100, 100.5, 101, 99)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ExitReason, Is.EqualTo(EndOfBacktestReason));
            Assert.That(row.ClosePrice, Is.EqualTo(100.5).Within(Tolerance));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_StopLossReachedOnEntryCandle_ClosesOnThatCandle(PositionDirection direction)
        {
            double s = Sign(direction);
            double stop = 100 - 10 * s;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 - 2 * s, stop, 100 + 1 * s),
                Flat(2, 100)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.OpenPrice, Is.EqualTo(100).Within(Tolerance));
            Assert.That(row.ClosePrice, Is.EqualTo(stop).Within(Tolerance));
            Assert.That(row.ExitReason, Is.EqualTo(StopLossReason));
            Assert.That(row.CloseTime, Is.EqualTo(ToUtc(T(1))));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_CandleGapsPastStop_FillsAtStopPrice(PositionDirection direction)
        {
            double s = Sign(direction);
            double stop = 100 - 10 * s;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 + 1 * s, 100 + 2 * s, 100 - 2 * s),
                Bar(2, 100 - 15 * s, 100 - 16 * s, 100 - 17 * s, 100 - 14 * s),
                Flat(3, 84)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ClosePrice, Is.EqualTo(stop).Within(Tolerance));
            Assert.That(row.ExitReason, Is.EqualTo(StopLossReason));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_NoTakeProfit_FavourableMoveDoesNotClose(PositionDirection direction)
        {
            double s = Sign(direction);
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop: 100 - 10 * s, takeProfit: null));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 + 1 * s, 100 + 2 * s, 100 - 2 * s),
                Bar(2, 100 + 1 * s, 100 + 2 * s, 100 + 50 * s, 100 - 1 * s),
                Flat(3, 100)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ExitReason, Is.EqualTo(EndOfBacktestReason));
        }

        [Test]
        public async Task StartAsync_PositionOpenAtEnd_ClosesAtLastCandleClose()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 101, 102, 99),
                Bar(2, 101, 103, 104, 100)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.ClosePrice, Is.EqualTo(103).Within(Tolerance));
            Assert.That(row.CloseTime, Is.EqualTo(ToUtc(T(2))));
            Assert.That(row.ExitReason, Is.EqualTo(EndOfBacktestReason));
        }

        [Test]
        public async Task StartAsync_TradeRow_IdentifiesRunAndEntry()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Short, stop: 110));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98), Flat(1, 100), Flat(2, 99)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.AssetPair, Is.EqualTo(AssetPair));
            Assert.That(row.Interval, Is.EqualTo(Interval));
            Assert.That(row.Strategy, Is.EqualTo(StrategyName));
            Assert.That(row.Position, Is.EqualTo(PositionDirection.Short));
            Assert.That(row.EntryReason, Is.EqualTo(EntryReason));
        }

        [Test]
        public async Task StartAsync_ShortTrade_OpenPriceIsEntryAndClosePriceIsExit()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Short, stop: 110))
                .SignalOn(T(2), Exit());
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 99, 101, 98),
                Bar(2, 98, 97, 99, 96),
                Bar(3, 95, 94, 96, 93),
                Flat(4, 94)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.OpenPrice, Is.EqualTo(100).Within(Tolerance));
            Assert.That(row.ClosePrice, Is.EqualTo(95).Within(Tolerance));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_ClosedTrade_PnLIsPriceDifferenceMinusFees(PositionDirection direction)
        {
            double s = Sign(direction);
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop: 100 - 10 * s))
                .SignalOn(T(2), Exit());
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 98),
                Bar(1, 100, 100 + 1 * s, 100 + 2 * s, 100 - 2 * s),
                Bar(2, 100 + 2 * s, 100 + 3 * s, 100 + 4 * s, 100 + 1 * s),
                Bar(3, 100 + 5 * s, 100 + 6 * s, 100 + 7 * s, 100 + 4 * s),
                Flat(4, 100 + 6 * s)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            double grossPnL = (row.ClosePrice - row.OpenPrice) * row.CoinAmount * s;
            Assert.That(row.Fees, Is.GreaterThan(0));
            Assert.That(row.PnL + row.Fees, Is.EqualTo(grossPnL).Within(Tolerance));
            Assert.That(grossPnL, Is.GreaterThan(0));
        }

        [Test]
        public async Task StartAsync_LongTrade_BoughtAmountIsCoinsTimesOpenPrice()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 101, 102, 99),
                Flat(2, 101)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.BoughtAmount, Is.EqualTo(row.CoinAmount * row.OpenPrice).Within(Tolerance));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_FirstTrade_SizesRiskAmountOverStopDistanceFromFillPrice(PositionDirection direction)
        {
            double s = Sign(direction);
            double fillPrice = 100;
            double stop = fillPrice - 10 * s;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, fillPrice, fillPrice + 1 * s, fillPrice + 2 * s, fillPrice - 2 * s),
                Flat(2, fillPrice)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            double expectedCoins = InitialBalance * RiskSize / Math.Abs(fillPrice - stop);
            Assert.That(row.CoinAmount, Is.EqualTo(expectedCoins).Within(Tolerance));
        }

        [Test]
        public async Task StartAsync_TightStop_SizeIsCappedByBalance()
        {
            double fillPrice = 100;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 99.9));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, fillPrice, 100.05, 100.08, 99.95),
                Bar(2, 100.02, 100.03, 100.06, 99.98)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            double expectedCoins = InitialBalance / (fillPrice * (1 + Fee));
            Assert.That(row.CoinAmount, Is.EqualTo(expectedCoins).Within(Tolerance));
            Assert.That(row.CoinAmount * row.OpenPrice * (1 + Fee), Is.LessThanOrEqualTo(InitialBalance + Tolerance));
        }

        [TestCase(95.0)]
        [TestCase(130.0)]
        public async Task StartAsync_SecondTrade_SizesFromBalanceAfterFirstTradePnL(double firstExitPrice)
        {
            double secondFillPrice = 200;
            double secondStop = 190;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90))
                .SignalOn(T(2), Exit())
                .SignalOn(T(3), Entry(PositionDirection.Long, stop: secondStop));
            List<OhlcvtCandle> candles = TwoTradeCandles(firstExitPrice, secondFillPrice);

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].PnL < 0, Is.EqualTo(firstExitPrice < 100));
            double balanceAfterFirstTrade = InitialBalance + rows[0].PnL;
            double expectedCoins = balanceAfterFirstTrade * RiskSize / (secondFillPrice - secondStop);
            Assert.That(rows[1].CoinAmount, Is.EqualTo(expectedCoins).Within(Tolerance));
        }

        [Test]
        public async Task StartAsync_TightStopAfterLoss_SizeIsCappedByCurrentBalance()
        {
            double secondFillPrice = 200;
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90))
                .SignalOn(T(2), Exit())
                .SignalOn(T(3), Entry(PositionDirection.Long, stop: 199.9));
            List<OhlcvtCandle> candles = TwoTradeCandles(95.0, secondFillPrice);
            candles[4] = Bar(4, secondFillPrice, 200.05, 200.08, 199.95);
            candles[5] = Bar(5, 200.02, 200.03, 200.06, 199.98);

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].PnL, Is.Negative);
            double balanceAfterFirstTrade = InitialBalance + rows[0].PnL;
            double expectedCoins = balanceAfterFirstTrade / (secondFillPrice * (1 + Fee));
            Assert.That(rows[1].CoinAmount, Is.EqualTo(expectedCoins).Within(Tolerance));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public async Task StartAsync_EntryCandleOpensPastStop_TakesNoTrade(PositionDirection direction)
        {
            double s = Sign(direction);
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(direction, stop: 100 - 1 * s));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 100),
                Flat(1, 100 - 2 * s),
                Flat(2, 100 - 2 * s)
            };

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Is.Empty);
        }

        [Test]
        public async Task StartAsync_SeveralTrades_AccountSizeIsInitialBalanceOnEveryRow()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90))
                .SignalOn(T(2), Exit())
                .SignalOn(T(3), Entry(PositionDirection.Long, stop: 190));
            List<OhlcvtCandle> candles = TwoTradeCandles(95.0, 200);

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows.Select(x => x.AccountSize), Is.All.EqualTo(InitialBalance));
        }

        [Test]
        public async Task StartAsync_EntrySignalWhilePositionOpen_IsIgnored()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Entry(PositionDirection.Long, stop: 90, reason: "First entry"))
                .SignalOn(T(2), Entry(PositionDirection.Long, stop: 104, reason: "Second entry"));
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 101, 102, 99),
                Bar(2, 102, 103, 104.5, 101),
                Bar(3, 105, 106, 107, 104.5),
                Flat(4, 106)
            };

            TradeRow row = SingleRow(await RunTradesAsync(strategy, candles));

            Assert.That(row.EntryReason, Is.EqualTo("First entry"));
            Assert.That(row.OpenPrice, Is.EqualTo(100).Within(Tolerance));
            Assert.That(row.CoinAmount, Is.EqualTo(InitialBalance * RiskSize / 10).Within(Tolerance));
            Assert.That(row.ExitReason, Is.EqualTo(EndOfBacktestReason));
        }

        [Test]
        public async Task StartAsync_ExitSignalWithoutPosition_IsIgnored()
        {
            FakeStrategy strategy = NewStrategy()
                .SignalOn(T(0), Exit());
            List<OhlcvtCandle> candles = new List<OhlcvtCandle>
            {
                Flat(0, 100), Flat(1, 101), Flat(2, 102), Flat(3, 103)
            };

            List<TradeRow> rows = await RunTradesAsync(strategy, candles);

            Assert.That(rows, Is.Empty);
        }

        private static List<OhlcvtCandle> TwoTradeCandles(double firstExitPrice, double secondFillPrice)
        {
            return new List<OhlcvtCandle>
            {
                Bar(0, 97, 98, 99, 96),
                Bar(1, 100, 101, 102, 99),
                Bar(2, 101, 102, 103, 100),
                Bar(3, firstExitPrice, firstExitPrice + 0.5, firstExitPrice + 1, firstExitPrice - 1),
                Bar(4, secondFillPrice, secondFillPrice + 1, secondFillPrice + 2, secondFillPrice - 1),
                Flat(5, secondFillPrice + 1)
            };
        }

        private static FakeStrategy NewStrategy()
        {
            return new FakeStrategy(StrategyName);
        }

        private static EntrySignal Entry(
            PositionDirection direction,
            double stop,
            double? takeProfit = null,
            string reason = EntryReason)
        {
            return new EntrySignal
            {
                Direction = direction,
                StopLossPrice = stop,
                TakeProfitPrice = takeProfit,
                Reason = reason
            };
        }

        private static ExitSignal Exit()
        {
            return new ExitSignal
            {
                Reason = ExitReason
            };
        }

        private static long T(int index)
        {
            return FromUnix + index * IntervalSeconds;
        }

        private static DateTime ToUtc(long timestamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
        }

        private static double Sign(PositionDirection direction)
        {
            return direction == PositionDirection.Long ? 1 : -1;
        }

        private static OhlcvtCandle Bar(int index, double open, double close, double extremeA, double extremeB)
        {
            double high = Math.Max(Math.Max(open, close), Math.Max(extremeA, extremeB));
            double low = Math.Min(Math.Min(open, close), Math.Min(extremeA, extremeB));

            return new OhlcvtCandle(T(index), open, high, low, close, 1, 1);
        }

        private static OhlcvtCandle Flat(int index, double price)
        {
            return Bar(index, price, price + 0.5, price + 1, price - 1);
        }

        private static TradeRow SingleRow(List<TradeRow> rows)
        {
            Assert.That(rows, Has.Count.EqualTo(1));

            return rows[0];
        }

        private static async Task<List<TradeRow>> RunTradesAsync(FakeStrategy strategy, List<OhlcvtCandle> candles)
        {
            Result<IEnumerable<TradeRow>> result = await RunAsync(strategy, new FakeMarketDataLoader(candles));

            Assert.That(result.IsSuccess, Is.True, () => result.Error.ToString());

            return result.Value.ToList();
        }

        private static async Task<Result<IEnumerable<TradeRow>>> RunAsync(
            FakeStrategy strategy,
            FakeMarketDataLoader loader)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.Configure<BacktesterConfiguration>(x => x.Fee = Fee);
            services.AddSingleton<IMarketDataLoader>(loader);

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            EngineConfiguration configuration = new EngineConfiguration(
                AssetPair,
                strategy.Name,
                From,
                To,
                Interval,
                RiskSize,
                InitialBalance);
            HistoricalDataEngine engine = new HistoricalDataEngine(configuration, strategy, scope);

            return await engine.StartAsync(CancellationToken.None);
        }
    }
}
