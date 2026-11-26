using System;
using TradeR.SharedKernel.Enums;
using TradeR.SharedKernel.Results;

namespace TradeR.SharedKernel.Indicators
{
    public sealed class VolumeWeightedAveragePrice
    {
        private readonly TimeOnly sessionOpen;
        private readonly AnchorPoint anchorPoint;

        private DateOnly currentDate;

        private double cumulativeVwap = 0;
        private double cumulativeVolume = 0;

        public double Vwap => cumulativeVwap / cumulativeVolume;

        private VolumeWeightedAveragePrice(
            TimeOnly sessionOpen,
            AnchorPoint anchorPoint)
        {
            this.sessionOpen = sessionOpen;
            this.anchorPoint = anchorPoint;
        }

        public static Result<VolumeWeightedAveragePrice> Create(
            TimeOnly sessionOpen,
            AnchorPoint anchorPoint)
        {
            return new VolumeWeightedAveragePrice(sessionOpen, anchorPoint);
        }

        public double? Add(
            double close,
            double high,
            double low,
            double volume,
            DateTime date)
        {
            if (cumulativeVwap == 0 && TimeOnly.FromDateTime(date) > sessionOpen)
            {
                throw new ArgumentException("The VWAP indicator needs the first candle at the start of the session!");
            }

            if (ShouldPriceReset(date))
            {
                Reset();
            }

            cumulativeVwap += (close + high + low) / 3 * volume;
            cumulativeVolume += volume;
            currentDate = DateOnly.FromDateTime(date);

            return Vwap;
        }

        public void Reset()
        {
            cumulativeVwap = 0;
            cumulativeVolume = 0;
        }

        private bool ShouldPriceReset(
            DateTime date)
            => anchorPoint switch
            {
                AnchorPoint.Session => DateOnly.FromDateTime(date) > currentDate &&
                                       TimeOnly.FromDateTime(date) >= sessionOpen,
                AnchorPoint.Week => date.DayOfWeek == DayOfWeek.Monday && currentDate.DayOfWeek == DayOfWeek.Sunday,
                AnchorPoint.Month => currentDate.Month == 12 ? date.Month < currentDate.Month : date.Month > currentDate.Month,
                _ => throw new ArgumentException("Invalid anchor point!", nameof(anchorPoint))
            };
    }
}
