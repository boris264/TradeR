using TradeR.Infrastructure.Models.Db;
using TradeR.Services.Models.Candles;

namespace TradeR.Infrastructure.Mappings
{
    public static class OhlcvtCandleMappings
    {
        public static OhlcvtCandle ToOhlcvtCandle(
            this KrakenOhlcvt source)
            => new OhlcvtCandle(
                source.Timestamp,
                source.Open,
                source.High,
                source.Low,
                source.Close,
                source.Volume,
                source.Trades);
    }
}
