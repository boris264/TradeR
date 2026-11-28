using TradeR.Services.Models.Market;

namespace TradeR.Infrastructure.Models.Dtos
{
    public class KrakenAssetPairTimeRange
    {
        public string AssetPair { get; set; } = null!;

        public MarketInterval IntervalMinutes { get; set; }

        public long OldestTime { get; set; }

        public long NewestTime { get; set; }
    }
}
