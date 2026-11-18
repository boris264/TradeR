using System;

namespace TradeR.Services.Models.Market
{
    public class MarketDataLoadRequest
    {
        public required string AssetPair { get; set; }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public MarketInterval Interval { get; set; }

        public int CandlesToPreload { get; set; }
    }
}
