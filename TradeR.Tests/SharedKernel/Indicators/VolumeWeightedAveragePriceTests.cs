using NUnit.Framework;
using System;
using TradeR.SharedKernel.Enums;
using TradeR.SharedKernel.Indicators;

namespace TradeR.Tests.SharedKernel.Indicators
{
    [TestFixture]
    public class VolumeWeightedAveragePriceTests
    {
        private readonly TimeOnly sessionOpen = TimeOnly.MinValue;
        private readonly TimeOnly sessionClose = TimeOnly.MaxValue;

        private static readonly TimeOnly nineThirty = new TimeOnly(9, 30);
        private static readonly TimeOnly sixteen = new TimeOnly(16, 0);

        [Test]
        public void Add_OneValueSessionAnchor_CalculatesVwapCorrectly()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sessionClose,
                AnchorPoint.Session).Value;

            var candleTime = DateTime.Today;

            var vwap = vwapIndicator.Add(
                2,
                3,
                1,
                10,
                candleTime);

            Assert.That(vwap, Is.Not.Zero);

            // VWAP Formula, ((high + close + low) / 3 * volume) / total volume
            Assert.That(vwap, Is.EqualTo(2));
        }

        [Test]
        public void Add_ThreeValuesSessionAnchor_CalculatesVwapCorrectlyWithAccumulation()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sessionClose,
                AnchorPoint.Session).Value;

            var candleTime = DateTime.Today;

            vwapIndicator.Add(
                2,
                3,
                1,
                10,
                candleTime);
            vwapIndicator.Add(
                4,
                6,
                2,
                5,
                candleTime);
            vwapIndicator.Add(
                6,
                10,
                5,
                30,
                candleTime);

            Assert.That(vwapIndicator.Vwap, Is.EqualTo(5.555555555555556).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_AddingSameCandlePrices_CalculatesFinalPriceCorrectly()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sessionClose,
                AnchorPoint.Session).Value;

            AddSamePrices(vwapIndicator, 1, 5, DateTime.Today);

            Assert.That(vwapIndicator.Vwap, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_AddingCandlesAfterSessionCloseOnSessionWithZeroVwap_ReturnsZero()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                nineThirty,
                sixteen,
                AnchorPoint.Session).Value;

            var vwapAfterSessionClose = AddSamePrices(vwapIndicator, 1, 1, DateTime.Today.AddHours(sixteen.Hour + 1));

            Assert.That(vwapAfterSessionClose, Is.Zero.Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_AddingCandlesAfterSessionClose_ReturnsPreviousVwap([Values] AnchorPoint anchorPoint)
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sixteen,
                anchorPoint).Value;

            var vwapBeforeSessionClose = AddSamePrices(vwapIndicator, 1, 5, DateTime.Today);
            var vwapAfterSessionClose = AddSamePrices(vwapIndicator, 3, 10, DateTime.Today.Add(sixteen.AddHours(1).ToTimeSpan()));

            Assert.That(vwapBeforeSessionClose, Is.EqualTo(vwapAfterSessionClose).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_AddingCandlesBeforeSessionOpen_ReturnsPreviousVwap([Values] AnchorPoint anchorPoint)
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                nineThirty,
                sixteen,
                anchorPoint).Value;

            var vwapBeforeSessionClose = AddSamePrices(vwapIndicator, 1, 5, DateTime.Today.Add(nineThirty.ToTimeSpan()));
            var vwapBeforeSessionOpen = AddSamePrices(vwapIndicator, 3, 10, DateTime.Today.AddDays(1).Add(nineThirty.AddHours(-1).ToTimeSpan()));

            Assert.That(vwapBeforeSessionClose, Is.EqualTo(vwapBeforeSessionOpen).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_FirstCandleOnANewWeek_ReturnsNewVwap()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sessionClose,
                AnchorPoint.Week).Value;

            var firstWeekVwap = AddSamePrices(vwapIndicator, 1, 5, Sunday());
            var secondWeekVwap = AddSamePrices(vwapIndicator, 3, 10, Sunday().AddDays(1));

            Assert.That(firstWeekVwap, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(secondWeekVwap, Is.EqualTo(3).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_CandlesOnANewWeek_ReturnsPreviousVwapUntilSessionOpen()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                nineThirty,
                sessionClose,
                AnchorPoint.Week).Value;

            var firstWeekVwap = AddSamePrices(vwapIndicator, 1, 5, Sunday(nineThirty));
            var secondWeekVwapBeforeOpen = AddSamePrices(vwapIndicator, 3, 10, Sunday().AddDays(1));
            var secondWeekVwapAfterOpen = AddSamePrices(vwapIndicator, 3, 10, Sunday(nineThirty).AddDays(1));

            Assert.That(firstWeekVwap, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(secondWeekVwapBeforeOpen, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(secondWeekVwapAfterOpen, Is.EqualTo(3).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_FirstCandleOnANewMonth_ReturnsNewVwap()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sessionClose,
                AnchorPoint.Month).Value;

            var firstMonthVwap = AddSamePrices(vwapIndicator, 1, 5, LastMonthlyDay());
            var secondMonthVwap = AddSamePrices(vwapIndicator, 3, 10, LastMonthlyDay().AddDays(1));

            Assert.That(firstMonthVwap, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(secondMonthVwap, Is.EqualTo(3).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_CandlesOnANewMonth_ReturnsPreviousVwapUntilSessionOpen()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                nineThirty,
                sessionClose,
                AnchorPoint.Month).Value;

            var firstMonthVwap = AddSamePrices(vwapIndicator, 1, 5, LastMonthlyDay(nineThirty));
            var secondMonthVwapBeforeOpen = AddSamePrices(vwapIndicator, 3, 10, LastMonthlyDay().AddDays(1));
            var secondMonthVwapAfterOpen = AddSamePrices(vwapIndicator, 3, 10, LastMonthlyDay(nineThirty).AddDays(1));

            Assert.That(firstMonthVwap, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(secondMonthVwapBeforeOpen, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(secondMonthVwapAfterOpen, Is.EqualTo(3).Within(IndicatorTestData.Tolerance));
        }

        [Test]
        public void Add_CandlesOnTheStartOfNewYear_ReturnsNewVwap()
        {
            var vwapIndicator = VolumeWeightedAveragePrice.Create(
                sessionOpen,
                sessionClose,
                AnchorPoint.Month).Value;

            var lastMonthVwap = AddSamePrices(vwapIndicator, 1, 5, new DateTime(2025, 12, 31));
            var nextYearVwap = AddSamePrices(vwapIndicator, 3, 10, new DateTime(2025, 12, 31).AddDays(1));

            Assert.That(lastMonthVwap, Is.EqualTo(1).Within(IndicatorTestData.Tolerance));
            Assert.That(nextYearVwap, Is.EqualTo(3).Within(IndicatorTestData.Tolerance));
        }

        private static double AddSamePrices(VolumeWeightedAveragePrice vwapIndicator, double price, double volume, DateTime date)
        {
            return vwapIndicator.Add(price, price, price, volume, date);
        }

        private static DateTime Sunday(TimeOnly? time = null)
        {
            var sunday = DateTime.Today.AddDays(-((int)DateTime.Now.DayOfWeek % 6));

            if (time != null)
            {
                sunday = sunday.Add(time.Value.ToTimeSpan());
            }

            return sunday;
        }

        private static DateTime LastMonthlyDay(TimeOnly? time = null)
        {
            var date = new DateTime(2025, 1, 31);

            if (time != null)
            {
                date = date.Add(time.Value.ToTimeSpan());
            }

            return date;
        }
    }
}
