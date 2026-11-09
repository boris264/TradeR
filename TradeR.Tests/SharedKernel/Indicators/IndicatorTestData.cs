using NUnit.Framework;
using System;

namespace TradeR.Tests.SharedKernel.Indicators
{
    /// <summary>Shared inputs and assertions for indicator tests.</summary>
    internal static class IndicatorTestData
    {
        public const double Tolerance = 1e-9;

        /// <summary>Fixed seed so every test run gets the same "random" prices and failures are reproducible.</summary>
        private const int DefaultSeed = 42;

        /// <summary>First price of the series, roughly a BTC/USD price, so values have realistic magnitude.</summary>
        private const double StartingPrice = 100_000;

        /// <summary>Largest move per step as a fraction of the price: 0.001 = up to ±0.1% per candle.</summary>
        private const double MaxStepChange = 0.001;

        /// <summary>
        /// A reproducible random walk: starts at <see cref="StartingPrice"/> and moves up or down by a random
        /// amount of up to <see cref="MaxStepChange"/> each step, like a series of candle closes.
        /// </summary>
        public static double[] RandomPrices(int count, int seed = DefaultSeed)
        {
            Random random = new Random(seed);
            double[] prices = new double[count];
            double price = StartingPrice;

            for (int i = 0; i < count; i++)
            {
                // NextDouble() is in [0, 1); mapping it to [-1, 1) gives a move of up to ±MaxStepChange.
                double change = ((random.NextDouble() * 2) - 1) * MaxStepChange;
                price *= 1 + change;
                prices[i] = price;
            }

            return prices;
        }

        /// <summary>Checks length, nulls and values (within <see cref="Tolerance"/>) element by element.</summary>
        public static void AssertSeries(double?[] actual, double?[] expected)
        {
            Assert.That(actual, Has.Length.EqualTo(expected.Length), "Series length");

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] is double value)
                {
                    Assert.That(actual[i], Is.EqualTo(value).Within(Tolerance), $"Value at index {i}");
                }
                else
                {
                    Assert.That(actual[i], Is.Null, $"Value at index {i}");
                }
            }
        }
    }
}
