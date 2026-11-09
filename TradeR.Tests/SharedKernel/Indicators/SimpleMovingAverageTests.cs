using System;
using System.Linq;
using NUnit.Framework;
using TradeR.SharedKernel.Indicators;
using TradeR.SharedKernel.Results;

namespace TradeR.Tests.SharedKernel.Indicators
{
    [TestFixture]
    public class SimpleMovingAverageTests
    {
        // ---- Create ----

        [TestCase(0)]
        [TestCase(-5)]
        public void Create_NonPositivePeriod_ReturnsInvalidPeriod(int period)
        {
            Result<SimpleMovingAverage> result = SimpleMovingAverage.Create(period);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Indicators.InvalidPeriod"));
        }

        [Test]
        public void Create_ValidPeriod_StartsEmpty()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(20).Value;

            Assert.That(sma.Period, Is.EqualTo(20));
            Assert.That(sma.Count, Is.Zero);
            Assert.That(sma.Current, Is.Null);
        }

        // ---- Add ----

        [Test]
        public void Add_FewerThanPeriodValues_ReturnsNull()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(3).Value;

            Assert.That(sma.Add(10), Is.Null);
            Assert.That(sma.Add(11), Is.Null);
        }

        [Test]
        public void Add_WindowJustFilled_ReturnsAverage()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(3).Value;
            sma.Add(10);
            sma.Add(11);

            double? result = sma.Add(12);

            // (10 + 11 + 12) / 3
            Assert.That(result, Is.EqualTo(11).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_MoreThanPeriodValues_SlidesWindow()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(3).Value;
            double[] values = { 10, 11, 12, 13, 14 };

            double?[] results = values.Select(sma.Add).ToArray();

            // Windows: [10,11,12] = 11, [11,12,13] = 12, [12,13,14] = 13
            IndicatorTestData.AssertSeries(results, new double?[] { null, null, 11, 12, 13 });
        }

        [Test]
        public void Add_PeriodOne_ReturnsEachValue()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(1).Value;

            Assert.That(sma.Add(10), Is.EqualTo(10).Within(IndicatorTestData.Tolerance));
            Assert.That(sma.Add(7.5), Is.EqualTo(7.5).Within(IndicatorTestData.Tolerance));
            Assert.That(sma.Add(42), Is.EqualTo(42).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_UpdatesCountAndCurrent()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(2).Value;

            sma.Add(10);
            Assert.That(sma.Count, Is.EqualTo(1));
            Assert.That(sma.Current, Is.Null);

            sma.Add(20);
            Assert.That(sma.Count, Is.EqualTo(2));
            Assert.That(sma.Current, Is.EqualTo(15).Within(IndicatorTestData.Tolerance));

            sma.Add(40);
            Assert.That(sma.Count, Is.EqualTo(3));
            Assert.That(sma.Current, Is.EqualTo(30).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_ManyValues_MatchesNaiveAverage()
        {
            // Guards the running sum against floating-point drift over a long series.
            const int period = 50;
            double[] prices = IndicatorTestData.RandomPrices(100_000);
            SimpleMovingAverage sma = SimpleMovingAverage.Create(period).Value;

            for (int i = 0; i < prices.Length; i++)
            {
                double? result = sma.Add(prices[i]);

                if (i >= period - 1 && i % 1_000 == 0)
                {
                    double naive = prices.Skip(i - period + 1).Take(period).Average();
                    Assert.That(result, Is.EqualTo(naive).Within(1e-6), $"Index {i}");
                }
            }
        }

        [Test]
        public void Reset_ClearsState_AndRestartsWarmUp()
        {
            SimpleMovingAverage sma = SimpleMovingAverage.Create(2).Value;
            sma.Add(100);
            sma.Add(200);

            sma.Reset();

            Assert.That(sma.Count, Is.Zero);
            Assert.That(sma.Current, Is.Null);
            Assert.That(sma.Add(10), Is.Null);
            // Only the values added after Reset count: (10 + 20) / 2
            Assert.That(sma.Add(20), Is.EqualTo(15).Within(IndicatorTestData.Tolerance));
        }

        // ---- Calculate ----

        [Test]
        public void Calculate_KnownSeries_ReturnsExpected()
        {
            double[] values = { 10, 11, 12, 11, 10 };

            Result<double?[]> result = SimpleMovingAverage.Calculate(values, 2);

            // Pairs: (10,11) = 10.5, (11,12) = 11.5, (12,11) = 11.5, (11,10) = 10.5
            Assert.That(result.IsSuccess, Is.True);
            IndicatorTestData.AssertSeries(result.Value, new double?[] { null, 10.5, 11.5, 11.5, 10.5 });
        }

        [Test]
        public void Calculate_ReturnsSameLengthAsInput_WithNullWarmUp()
        {
            double[] values = IndicatorTestData.RandomPrices(30);

            double?[] result = SimpleMovingAverage.Calculate(values, 10).Value;

            Assert.That(result, Has.Length.EqualTo(values.Length));
            Assert.That(result.Take(9), Is.All.Null);
            Assert.That(result.Skip(9), Is.All.Not.Null);
        }

        [Test]
        public void Calculate_ExactlyPeriodValues_OnlyLastHasValue()
        {
            double[] values = { 2, 4, 6 };

            double?[] result = SimpleMovingAverage.Calculate(values, 3).Value;

            IndicatorTestData.AssertSeries(result, new double?[] { null, null, 4 });
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Calculate_NonPositivePeriod_ReturnsInvalidPeriod(int period)
        {
            Result<double?[]> result = SimpleMovingAverage.Calculate(new double[] { 1, 2, 3 }, period);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Indicators.InvalidPeriod"));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Calculate_FewerValuesThanPeriod_ReturnsNotEnoughData(int count)
        {
            double[] values = Enumerable.Range(1, count).Select(i => (double)i).ToArray();

            Result<double?[]> result = SimpleMovingAverage.Calculate(values, 3);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("Indicators.NotEnoughData"));
        }

        [Test]
        public void Calculate_MatchesCandleByCandleAdd()
        {
            double[] prices = IndicatorTestData.RandomPrices(10_000);
            SimpleMovingAverage sma = SimpleMovingAverage.Create(20).Value;

            double?[] streamed = prices.Select(sma.Add).ToArray();
            double?[] batch = SimpleMovingAverage.Calculate(prices, 20).Value;

            Assert.That(batch, Is.EqualTo(streamed));
        }
    }
}
