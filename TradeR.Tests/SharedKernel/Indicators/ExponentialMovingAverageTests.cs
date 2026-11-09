using System.Linq;
using NUnit.Framework;
using TradeR.SharedKernel.Indicators;
using TradeR.SharedKernel.Results;

namespace TradeR.Tests.SharedKernel.Indicators
{
    [TestFixture]
    public class ExponentialMovingAverageTests
    {
        // ---- Create ----

        [TestCase(0)]
        [TestCase(-5)]
        public void Create_NonPositivePeriod_ReturnsInvalidPeriod(int period)
        {
            Result<ExponentialMovingAverage> result = ExponentialMovingAverage.Create(period);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Indicators.InvalidPeriod"));
        }

        [Test]
        public void Create_ValidPeriod_StartsEmpty()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(20).Value;

            Assert.That(ema.Period, Is.EqualTo(20));
            Assert.That(ema.Count, Is.Zero);
            Assert.That(ema.Current, Is.Null);
        }

        // ---- Add ----

        [Test]
        public void Add_FewerThanPeriodValues_ReturnsNull()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(3).Value;

            Assert.That(ema.Add(10), Is.Null);
            Assert.That(ema.Add(11), Is.Null);
        }

        [Test]
        public void Add_PeriodthValue_ReturnsSimpleAverageAsSeed()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(3).Value;
            ema.Add(10);
            ema.Add(11);

            double? result = ema.Add(12);

            // Seed = SMA of the first 3 values: (10 + 11 + 12) / 3
            Assert.That(result, Is.EqualTo(11).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_AfterSeed_AppliesSmoothing()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(3).Value;
            double[] values = { 10, 11, 12, 15, 14 };

            double?[] results = values.Select(ema.Add).ToArray();

            // k = 2 / (3 + 1) = 0.5; seed 11
            // 15: (15 - 11) * 0.5 + 11 = 13
            // 14: (14 - 13) * 0.5 + 13 = 13.5
            IndicatorTestData.AssertSeries(results, new double?[] { null, null, 11, 13, 13.5 });
        }

        [Test]
        public void Add_PeriodOne_ReturnsEachValue()
        {
            // k = 2 / (1 + 1) = 1, so the EMA jumps fully to every new value.
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(1).Value;

            Assert.That(ema.Add(10), Is.EqualTo(10).Within(IndicatorTestData.Tolerance));
            Assert.That(ema.Add(7.5), Is.EqualTo(7.5).Within(IndicatorTestData.Tolerance));
            Assert.That(ema.Add(42), Is.EqualTo(42).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_ConstantSeries_StaysConstant()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(5).Value;

            double?[] results = Enumerable.Repeat(50.0, 20).Select(ema.Add).ToArray();

            Assert.That(results.Take(4), Is.All.Null);
            Assert.That(results.Skip(4), Is.All.EqualTo(50).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_UpdatesCountAndCurrent()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(2).Value;

            ema.Add(10);
            Assert.That(ema.Count, Is.EqualTo(1));
            Assert.That(ema.Current, Is.Null);

            ema.Add(20);
            Assert.That(ema.Count, Is.EqualTo(2));
            Assert.That(ema.Current, Is.EqualTo(15).Within(IndicatorTestData.Tolerance));

            // k = 2/3: (30 - 15) * 2/3 + 15 = 25
            ema.Add(30);
            Assert.That(ema.Count, Is.EqualTo(3));
            Assert.That(ema.Current, Is.EqualTo(25).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Reset_ClearsState_AndRestartsWarmUp()
        {
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(2).Value;
            ema.Add(100);
            ema.Add(200);
            ema.Add(300);

            ema.Reset();

            Assert.That(ema.Count, Is.Zero);
            Assert.That(ema.Current, Is.Null);
            Assert.That(ema.Add(10), Is.Null);
            // Re-seeded from the new values only: (10 + 20) / 2, not continued from the old EMA
            Assert.That(ema.Add(20), Is.EqualTo(15).Within(IndicatorTestData.Tolerance));
        }

        // ---- Calculate ----

        [Test]
        public void Calculate_KnownSeries_ReturnsExpected()
        {
            double[] values = { 10, 11, 12, 11, 10 };

            Result<double?[]> result = ExponentialMovingAverage.Calculate(values, 2);

            // k = 2 / (2 + 1) = 2/3; seed (10 + 11) / 2 = 10.5
            // 12: (12 - 10.5) * 2/3 + 10.5 = 11.5
            // 11: (11 - 11.5) * 2/3 + 11.5 = 67/6  ≈ 11.1667
            // 10: (10 - 67/6) * 2/3 + 67/6 = 187/18 ≈ 10.3889
            Assert.That(result.IsSuccess, Is.True);
            IndicatorTestData.AssertSeries(result.Value, new double?[] { null, 10.5, 11.5, 67.0 / 6, 187.0 / 18 });
        }

        [Test]
        public void Calculate_ReturnsSameLengthAsInput_WithNullWarmUp()
        {
            double[] values = IndicatorTestData.RandomPrices(30);

            double?[] result = ExponentialMovingAverage.Calculate(values, 10).Value;

            Assert.That(result, Has.Length.EqualTo(values.Length));
            Assert.That(result.Take(9), Is.All.Null);
            Assert.That(result.Skip(9), Is.All.Not.Null);
        }

        [Test]
        public void Calculate_ExactlyPeriodValues_OnlyLastHasValue()
        {
            double[] values = { 2, 4, 6 };

            double?[] result = ExponentialMovingAverage.Calculate(values, 3).Value;

            // Only the seed exists: (2 + 4 + 6) / 3
            IndicatorTestData.AssertSeries(result, new double?[] { null, null, 4 });
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Calculate_NonPositivePeriod_ReturnsInvalidPeriod(int period)
        {
            Result<double?[]> result = ExponentialMovingAverage.Calculate(new double[] { 1, 2, 3 }, period);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Indicators.InvalidPeriod"));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Calculate_FewerValuesThanPeriod_ReturnsNotEnoughData(int count)
        {
            double[] values = Enumerable.Range(1, count).Select(i => (double)i).ToArray();

            Result<double?[]> result = ExponentialMovingAverage.Calculate(values, 3);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Indicators.NotEnoughData"));
        }

        [Test]
        public void Calculate_MatchesCandleByCandleAdd()
        {
            double[] prices = IndicatorTestData.RandomPrices(10_000);
            ExponentialMovingAverage ema = ExponentialMovingAverage.Create(20).Value;

            double?[] streamed = prices.Select(ema.Add).ToArray();
            double?[] batch = ExponentialMovingAverage.Calculate(prices, 20).Value;

            Assert.That(batch, Is.EqualTo(streamed));
        }
    }
}
