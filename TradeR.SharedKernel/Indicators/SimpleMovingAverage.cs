using System;
using TradeR.SharedKernel.Results;

namespace TradeR.SharedKernel.Indicators
{
    /// <summary>
    /// Simple moving average: the average of the last <see cref="Period"/> values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use it candle by candle with <see cref="Add"/>: it keeps a moving window of the last <see cref="Period"/> values,
    /// and each call only uses the value passed in and the ones before it, so it can't look ahead.
    /// Each returned value is a plain number; later calls move the window but never change values already returned.
    /// </para>
    /// <para>
    /// Or calculate a whole series at once with <see cref="Calculate"/>, which runs the same logic and returns the same numbers.
    /// </para>
    /// <code>
    /// SimpleMovingAverage sma20 = SimpleMovingAverage.Create(20).Value;
    /// foreach (double close in closes)
    /// {
    ///     if (sma20.Add(close) is double sma) { /* use sma */ }
    /// }
    /// </code>
    /// </remarks>
    public sealed class SimpleMovingAverage
    {
        // Ring buffer holding the last Period values. Values are never shifted; the oldest slot is overwritten instead.
        private readonly double[] _window;

        // Slot the next value is written to. Once the window is full, this slot holds the oldest value.
        private int _next;

        // Sum of the values currently in _window, so each Add only adjusts it instead of re-adding the whole window.
        private double _sum;

        private SimpleMovingAverage(int period)
        {
            Period = period;
            _window = new double[period];
        }

        /// <summary>Number of values averaged.</summary>
        public int Period { get; }

        /// <summary>Number of values added so far.</summary>
        public long Count { get; private set; }

        /// <summary>The latest average, or <c>null</c> until <see cref="Period"/> values have been added.</summary>
        public double? Current { get; private set; }

        public static Result<SimpleMovingAverage> Create(int period)
        {
            if (period <= 0)
            {
                return IndicatorErrors.InvalidPeriod(period);
            }

            return new SimpleMovingAverage(period);
        }

        /// <summary>
        /// Calculates the SMA for every value. The result has the same length as <paramref name="values"/>, so index
        /// <c>i</c> is the SMA at value <c>i</c>; the first <c>period - 1</c> entries are <c>null</c>.
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

            SimpleMovingAverage sma = new SimpleMovingAverage(period);
            double?[] result = new double?[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                result[i] = sma.Add(values[i]);
            }

            return result;
        }

        /// <summary>
        /// Moves the window forward by one value and returns the new average, or <c>null</c> while fewer than
        /// <see cref="Period"/> values have been added.
        /// </summary>
        public double? Add(double value)
        {
            // Once the window is full, the value about to be overwritten is the oldest one: remove it from the sum.
            // (>= rather than >: the value that fills the window exactly is the first one that pushes another out.)
            if (Count >= Period)
            {
                _sum -= _window[_next];
            }

            // Put the new value in the freed slot and add it to the sum.
            _window[_next] = value;
            _sum += value;
            Count++;

            // Move to the next slot, wrapping to the start after the last one.
            _next++;
            if (_next == Period)
            {
                _next = 0;

                // Recompute the sum once per full window so rounding errors from add/subtract can't build up
                // over millions of values. Costs O(period) every period values, so still O(1) per value on average.
                _sum = Sum(_window);
            }

            // No average until the window has been filled once.
            Current = Count >= Period ? _sum / Period : null;
            return Current;
        }

        /// <summary>Clears all values so the instance can be reused.</summary>
        public void Reset()
        {
            Array.Clear(_window);
            _next = 0;
            _sum = 0;
            Count = 0;
            Current = null;
        }

        private static double Sum(double[] values)
        {
            double sum = 0;
            foreach (double value in values)
            {
                sum += value;
            }

            return sum;
        }
    }
}
