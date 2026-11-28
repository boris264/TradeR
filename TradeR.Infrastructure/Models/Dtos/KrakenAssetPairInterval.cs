namespace TradeR.Infrastructure.Models.Dtos
{
    public class KrakenAssetPairInterval
    {
        public required string AssetPair { get; set; }

        public int IntervalMinutes { get; set; }
    }
}
