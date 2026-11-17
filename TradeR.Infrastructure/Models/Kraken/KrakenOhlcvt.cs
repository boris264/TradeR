namespace TradeR.Infrastructure.Models.Kraken
{
    public class KrakenOhlcvt
    {
        public string AssetPair { get; set; } = null!;

        public int IntervalMinutes { get; set; }

        public long Timestamp { get; set; }

        public double Open { get; set; }

        public double High { get; set; }

        public double Low { get; set; }

        public double Close { get; set; }

        public double Volume { get; set; }

        public int Trades { get; set; }
    }
}
