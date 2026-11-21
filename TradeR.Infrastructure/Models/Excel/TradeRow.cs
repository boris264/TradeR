using System;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Enums;

namespace TradeR.Infrastructure.Models.Excel
{
    public class TradeRow
    {
        public required string AssetPair { get; set; }

        public MarketInterval Interval { get; set; }

        public required string Strategy { get; set; }

        public PositionDirection Position { get; set; }

        public double CoinAmount { get; set; }

        public double BoughtAmount { get; set; }

        public string? Currency { get; set; }

        public double BuyPrice { get; set; }

        public double SellPrice { get; set; }

        public double Fees { get; set; }

        public double PnL { get; set; }

        public DateTime OpenTime { get; set; }

        public DateTime CloseTime { get; set; }

        public string EntryReason { get; set; } = string.Empty;

        public string ExitReason { get; set; } = string.Empty;
    }
}
