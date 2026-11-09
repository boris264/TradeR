using System;
using TradeR.SharedKernel.Enums;

namespace TradeR.SharedKernel.Common
{
    public class PnLCalculator
    {
        // Fee charged on the value of every order: 0.01 = 1%.
        private readonly double fee = 0.01;

        public PnLCalculator(double fee)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fee);

            if (fee > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fee));
            }

            this.fee = fee;
        }

        /// <summary>
        /// Returns the P&amp;L, after fees, of closing <paramref name="closingPositionSize"/> coins
        /// of a position that was opened with <paramref name="openPositionSize"/> coins.
        /// A partial close pays only its own share of the opening fee, so closing a position
        /// in several parts gives the same total as closing it all at once.
        /// </summary>
        public double Calculate(
            double openingPrice,
            double openPositionSize,
            double closingPrice,
            double closingPositionSize,
            PositionDirection positionDirection)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(openingPrice);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(openPositionSize);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(closingPrice);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(closingPositionSize);

            if (closingPositionSize > openPositionSize)
            {
                throw new ArgumentException(
                    "Cannot close more coins than the position was opened with.",
                    nameof(closingPositionSize));
            }

            // Only the closed coins count: their share of the opening order, and the closing order.
            double openingValue = closingPositionSize * openingPrice;
            double closingValue = closingPositionSize * closingPrice;

            // A long profits when the price rises, a short when it falls.
            double direction = positionDirection == PositionDirection.Long ? 1.0 : -1.0;
            double pnLBeforeFees = direction * (closingValue - openingValue);

            double fees = fee * (openingValue + closingValue);

            return pnLBeforeFees - fees;
        }
    }
}
