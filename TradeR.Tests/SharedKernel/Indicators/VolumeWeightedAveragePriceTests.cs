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
        private readonly TimeOnly sessionClose = TimeOnly.MinValue;

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

            Assert.That(vwap, Is.Not.Null);

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
    }
}
