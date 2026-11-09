using System;
using TradeR.SharedKernel.Results;

namespace TradeR.SharedKernel.Indicators
{
    /// <summary>
    /// Exponential moving average with smoothing factor <c>k = 2 / (period + 1)</c>, seeded with the simple average of
    /// the first <see cref="Period"/> values (the same seeding as TradingView's <c>ta.ema</c> and TA-Lib).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use it candle by candle with <see cref="Add"/>, or calculate a whole series with <see cref="Calculate"/>; both give
    /// the same numbers, and each value only uses the values up to and including its own position.
    /// </para>
    /// <para>
    /// An EMA depends on where it starts: starting from a different first value gives slightly different results, which
    /// converge after roughly 3–4 × the period. Start it on enough history before the values you care about.
    /// </para>
    /// <code>
    /// ExponentialMovingAverage ema20 = ExponentialMovingAverage.Create(20).Value;
    /// foreach (double close in closes)
    /// {
    ///     if (ema20.Add(close) is double ema) { /* use ema */ }
    /// }
    /// </code>
    /// </remarks>
    public sealed class ExponentialMovingAverage
    {
        // Smoothing factor k: how far the EMA moves toward each new value. A longer period means a smaller k and a slower EMA.
        private readonly double _multiplier;

        // Sum of the first Period values, only used to seed the EMA with their simple average.
        private double _warmUpSum;

        private ExponentialMovingAverage(int period)
        {
            Period = period;
            _multiplier = 2.0 / (period + 1);
        }

        /// <summary>The EMA period.</summary>
        public int Period { get; }

        /// <summary>Number of values added so far.</summary>
        public long Count { get; private set; }

        /// <summary>The latest EMA, or <c>null</c> until <see cref="Period"/> values have been added.</summary>
        public double? Current { get; private set; }

        public static Result<ExponentialMovingAverage> Create(int period)
        {
            if (period <= 0)
            {
                return IndicatorErrors.InvalidPeriod(period);
            }

            return new ExponentialMovingAverage(period);
        }

        /// <summary>
        /// Calculates the EMA for every value. The result has the same length as <paramref name="values"/>, so index
        /// <c>i</c> is the EMA at value <c>i</c>; the first <c>period - 1</c> entries are <c>null</c>.
        /// </summary>
        public static Result<double?[]> Calculate(ReadOnlySpan<double> values, int period)
        {
            if (period <= 0)
            {
                return IndicatorErrors.InvalidPeriod(period);
            }

            if (values.Length < period)
            {
                return IndicatorErrors.NotEnoughData(values.Length, period);
            }

            ExponentialMovingAverage ema = new ExponentialMovingAverage(period);
            double?[] result = new double?[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                result[i] = ema.Add(values[i]);
            }

            return result;
        }

        /// <summary>
        /// Adds the next value and returns the new EMA, or <c>null</c> while fewer than <see cref="Period"/> values have
        /// been added.
        /// </summary>
        public double? Add(double value)
        {
            Count++;

            // Warm-up: only collect values; there is no EMA yet.
            if (Count < Period)
            {
                _warmUpSum += value;
                return null;
            }

            // Seed: with no previous EMA to weight against, the first EMA is the simple average of the first Period values.
            if (Count == Period)
            {
                _warmUpSum += value;
                Current = _warmUpSum / Period;
                return Current;
            }

            // Weighted: move the previous EMA a fraction k of the way toward the new value.
            // Same as k * value + (1 - k) * previous, rearranged: (value - previous) is the gap to close.
            double previous = Current!.Value;
            Current = (value - previous) * _multiplier + previous;
            return Current;
        }

        /// <summary>Clears all values so the instance can be reused.</summary>
        public void Reset()
        {
            _warmUpSum = 0;
            Count = 0;
            Current = null;
        }
    }
}
