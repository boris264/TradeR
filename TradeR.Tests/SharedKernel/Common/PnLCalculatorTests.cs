using NUnit.Framework;
using System;
using TradeR.SharedKernel.Common;
using TradeR.SharedKernel.Enums;

namespace TradeR.Tests.SharedKernel.Common
{
    // Expected values are worked out by hand with a 1% fee on each order's value,
    // so a wrong fee formula in the calculator makes these tests fail.
    [TestFixture]
    public class PnLCalculatorTests
    {
        private const double Tolerance = 1e-9;

        private readonly PnLCalculator pnLCalculator = new PnLCalculator(fee: 0.01);

        // 500 profit - (10 + 15) fees
        [TestCase(1000, 1, 1500, 1, 475.0)]
        // 0 profit - (10 + 10) fees
        [TestCase(1000, 1, 1000, 1, -20.0)]
        // -500 loss - (10 + 5) fees
        [TestCase(1000, 1, 500, 1, -515.0)]
        public void Calculate_LongFullClose_ReturnsPnLAfterFees(
            double openingPrice,
            double openPositionSize,
            double closingPrice,
            double closingPositionSize,
            double expectedPnL)
        {
            double result = pnLCalculator.Calculate(
                openingPrice, openPositionSize, closingPrice, closingPositionSize, PositionDirection.Long);

            Assert.That(result, Is.EqualTo(expectedPnL).Within(Tolerance));
        }

        // 500 profit - (10 + 5) fees
        [TestCase(1000, 1, 500, 1, 485.0)]
        // 0 profit - (10 + 10) fees
        [TestCase(1000, 1, 1000, 1, -20.0)]
        // -500 loss - (5 + 10) fees
        [TestCase(500, 1, 1000, 1, -515.0)]
        public void Calculate_ShortFullClose_ReturnsPnLAfterFees(
            double openingPrice,
            double openPositionSize,
            double closingPrice,
            double closingPositionSize,
            double expectedPnL)
        {
            double result = pnLCalculator.Calculate(
                openingPrice, openPositionSize, closingPrice, closingPositionSize, PositionDirection.Short);

            Assert.That(result, Is.EqualTo(expectedPnL).Within(Tolerance));
        }

        // 0.5 * (2000 - 1000) = 500 profit - (5 + 10) fees
        [TestCase(1000, 1, 2000, 0.5, 485.0)]
        // 0.5 * (1100 - 1000) = 50 profit - (5 + 5.5) fees
        [TestCase(1000, 1, 1100, 0.5, 39.5)]
        public void Calculate_LongPartialClose_ChargesOnlyClosedShareOfOpeningFee(
            double openingPrice,
            double openPositionSize,
            double closingPrice,
            double closingPositionSize,
            double expectedPnL)
        {
            double result = pnLCalculator.Calculate(
                openingPrice, openPositionSize, closingPrice, closingPositionSize, PositionDirection.Long);

            Assert.That(result, Is.EqualTo(expectedPnL).Within(Tolerance));
        }

        // 0.5 * (1000 - 500) = 250 profit - (5 + 2.5) fees
        [TestCase(1000, 1, 500, 0.5, 242.5)]
        // 1 * (1000 - 1200) = -200 loss - (10 + 12) fees
        [TestCase(1000, 2, 1200, 1, -222.0)]
        public void Calculate_ShortPartialClose_ChargesOnlyClosedShareOfOpeningFee(
            double openingPrice,
            double openPositionSize,
            double closingPrice,
            double closingPositionSize,
            double expectedPnL)
        {
            double result = pnLCalculator.Calculate(
                openingPrice, openPositionSize, closingPrice, closingPositionSize, PositionDirection.Short);

            Assert.That(result, Is.EqualTo(expectedPnL).Within(Tolerance));
        }

        [TestCase(PositionDirection.Long)]
        [TestCase(PositionDirection.Short)]
        public void Calculate_ClosedInTwoHalves_EqualsClosedAtOnce(PositionDirection positionDirection)
        {
            double fullClose = pnLCalculator.Calculate(1000, 1, 1500, 1, positionDirection);

            double firstHalf = pnLCalculator.Calculate(1000, 1, 1500, 0.5, positionDirection);
            double secondHalf = pnLCalculator.Calculate(1000, 0.5, 1500, 0.5, positionDirection);

            Assert.That(firstHalf + secondHalf, Is.EqualTo(fullClose).Within(Tolerance));
        }

        [Test]
        public void Calculate_ClosingMoreThanOpened_ThrowsArgumentException()
        {
            Assert.That(
                () => pnLCalculator.Calculate(1000, 1, 1000, 2, PositionDirection.Long),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(0, 1, 1000, 1)]
        [TestCase(-1000, 1, 1000, 1)]
        [TestCase(1000, 0, 1000, 1)]
        [TestCase(1000, 1, 0, 1)]
        [TestCase(1000, 1, 1000, 0)]
        [TestCase(1000, 1, 1000, -1)]
        public void Calculate_NonPositiveInput_ThrowsArgumentOutOfRangeException(
            double openingPrice,
            double openPositionSize,
            double closingPrice,
            double closingPositionSize)
        {
            Assert.That(
                () => pnLCalculator.Calculate(
                    openingPrice, openPositionSize, closingPrice, closingPositionSize, PositionDirection.Long),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
