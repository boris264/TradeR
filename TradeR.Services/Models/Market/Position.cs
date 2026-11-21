using System;
using TradeR.SharedKernel.Enums;

namespace TradeR.Services.Models.Market
{
    public sealed class Position
    {
        public PositionDirection Direction { get; init; }

        public Order OpenOrder { get; private set; }

        public Order? CloseOrder { get; private set; }

        public Order? TakeProfit { get; private set; }

        public Order? StopLoss { get; private set; }

        public long OpenTimestamp { get; private set; }

        public long CloseTimestamp { get; private set; }

        public string EntryReason { get; private set; }

        public string? ExitReason { get; private set; }

        public Position(
            PositionDirection direction,
            Order openingOrder,
            long openingTimestamp,
            string entryReason)
        {
            Direction = direction;
            OpenOrder = openingOrder;
            OpenTimestamp = openingTimestamp;
            EntryReason = entryReason;
        }

        public void Close(Order closeOrder, long timestamp, string exitReason)
        {
            if (Direction == PositionDirection.Long && closeOrder.Type != OrderType.Sell)
            {
                throw new ArgumentException("Closing a Long position requires a Sell order!", nameof(closeOrder));
            }

            if (OpenTimestamp < timestamp)
            {
                throw new ArgumentException(
                    "Opening timestamp of the position cannot be less than the closing timestamp",
                    nameof(timestamp));
            }

            CloseOrder = closeOrder;
            CloseTimestamp = timestamp;
            ExitReason = exitReason;
        }

        public void PlaceStopLoss(double amount, double stopPrice)
        {
            StopLoss = new Order()
            {
                Type = Direction == PositionDirection.Long
                           ? OrderType.Sell
                           : OrderType.Buy,
                Amount = amount,
                Price = stopPrice,
            };
        }

        public void PlaceTakeProfit(double amount, double takeProfitPrice)
        {
            TakeProfit = new Order()
            {
                Type = Direction == PositionDirection.Long
                           ? OrderType.Sell
                           : OrderType.Buy,
                Amount = amount,
                Price = takeProfitPrice,
            };
        }
    }
}
