using System;
using TradeR.SharedKernel.Enums;
using TradeR.SharedKernel.Results;

namespace TradeR.SharedKernel.Indicators
{
    public sealed class VolumeWeightedAveragePrice
    {
        private readonly TimeOnly sessionOpen;
        private readonly TimeOnly sessionClose;
        private readonly AnchorPoint anchorPoint;

        private DateOnly currentDate;

        private double cumulativeVwap = 0;
        private double cumulativeVolume = 0;

        public double Vwap => cumulativeVwap / cumulativeVolume;

        private VolumeWeightedAveragePrice(
            TimeOnly sessionOpen,
            TimeOnly sessionClose,
            AnchorPoint anchorPoint)
        {
            this.sessionOpen = sessionOpen;
            this.sessionClose = sessionClose;
            this.anchorPoint = anchorPoint;
        }

        public static Result<VolumeWeightedAveragePrice> Create(
            TimeOnly sessionOpen,
            TimeOnly sessionClose,
            AnchorPoint anchorPoint)
        {
            return new VolumeWeightedAveragePrice(sessionOpen, sessionClose, anchorPoint);
        }

        public double Add(
            double close,
            double high,
            double low,
            double volume,
            DateTime date)
        {
            var dateToTime = TimeOnly.FromDateTime(date);
            if (!IsSession24Hours() && (dateToTime < sessionOpen || dateToTime >= sessionClose))
            {
                return cumulativeVwap == 0 ? 0 : Vwap;
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

        private void Reset()
        {
            cumulativeVwap = 0;
            cumulativeVolume = 0;
        }

        private bool ShouldPriceReset(
            DateTime date)
        {
            var dateToDateOnly = DateOnly.FromDateTime(date);
            return anchorPoint switch
            {
                AnchorPoint.Session => dateToDateOnly > currentDate,
                AnchorPoint.Week => date.DayOfWeek == DayOfWeek.Monday && currentDate.DayOfWeek == DayOfWeek.Sunday,
                AnchorPoint.Month => currentDate.Month == 12 ? date.Month < currentDate.Month : date.Month > currentDate.Month,
                _ => throw new ArgumentException("Invalid anchor point!")
            };
        }

        private bool IsSession24Hours()
            => sessionOpen == TimeOnly.MinValue && sessionClose == TimeOnly.MaxValue;
    }
}
