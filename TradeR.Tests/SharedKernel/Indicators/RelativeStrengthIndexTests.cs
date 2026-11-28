using NUnit.Framework;
using System.Collections.Generic;
using TradeR.SharedKernel.Indicators;

namespace TradeR.Tests.SharedKernel.Indicators
{
    [TestFixture]
    public class RelativeStrengthIndexTests
    {
        // RSI Calculation Sheet: https://www.litefinance.org/blog/for-beginners/best-technical-indicators/rsi-relative-strength-index/
        private readonly List<KeyValuePair<double, double?>> pricesWithExpectedRsiValues = new()
        {
            new KeyValuePair<double, double?>( 245.67, null),
            new KeyValuePair<double, double?>( 248.65, null ),
            new KeyValuePair<double, double?>( 249.11, null ),
            new KeyValuePair<double, double?>( 252.67, null ),
            new KeyValuePair<double, double?>( 247.58, null ),
            new KeyValuePair<double, double?>( 248.98, null ),
            new KeyValuePair<double, double?>( 246.23, null ),
            new KeyValuePair<double, double?>( 245.67, null ),
            new KeyValuePair<double, double?>( 243.70, null ),
            new KeyValuePair<double, double?>( 245.76, null ),
            new KeyValuePair<double, double?>( 244.73, null ),
            new KeyValuePair<double, double?>( 247.45, null ),
            new KeyValuePair<double, double?>( 248.76, null ),
            new KeyValuePair<double, double?>( 251.55, null ),
            new KeyValuePair<double, double?>( 254.98, 64.50),
            new KeyValuePair<double, double?>( 257.30, 67.06),
            new KeyValuePair<double, double?>( 256.96, 66.30),
            new KeyValuePair<double, double?>( 256.20, 64.55),
            new KeyValuePair<double, double?>( 257.74, 66.48),
            new KeyValuePair<double, double?>( 254.23, 58.64),
            new KeyValuePair<double, double?>( 251.88, 54.05),
            new KeyValuePair<double, double?>( 248.65, 48.43),
            new KeyValuePair<double, double?>( 245.67, 43.90),
            new KeyValuePair<double, double?>( 244.23, 41.86),
            new KeyValuePair<double, double?>( 241.34, 38.04),
            new KeyValuePair<double, double?>( 239.99, 36.37),
            new KeyValuePair<double, double?>( 242.72, 41.92),
            new KeyValuePair<double, double?>( 243.22, 42.90),
            new KeyValuePair<double, double?>( 248.33, 51.86),
            new KeyValuePair<double, double?>( 247.98, 51.27)
        };

        [Test]
        public void Add_FirstFifteenCandles_ReturnsNullUntilTheLastCandle()
        {
            var rsi = new RelativeStrengthIndex();

            for (int i = 0; i < 14; i++)
            {
                var rsiVal = rsi.Add(i);

                Assert.That(rsiVal, Is.Null);
            }

            var lastVal = rsi.Add(15);

            Assert.That(lastVal, Is.Not.Null);
        }

        [Test]
        public void Add_PricesWithExpectedValues_ReturnsValidRsiForEachValue()
        {
            var rsi = new RelativeStrengthIndex();

            foreach (var kv in pricesWithExpectedRsiValues)
            {
                var value = rsi.Add(kv.Key);

                Assert.That(value, Is.EqualTo(kv.Value).Within(IndicatorTestData.Tolerance));
            }
        }

        [Test]
        public void Add_OnlyUptrendPrices_DoesntReturnNaN()
        {
            var rsi = new RelativeStrengthIndex();

            foreach (var kv in pricesWithExpectedRsiValues)
            {
                var value = rsi.Add(kv.Key);

                Assert.That(value, Is.EqualTo(kv.Value).Within(IndicatorTestData.Tolerance));
            }
        }
    }
}
