namespace TradeR.Infrastructure.Models.Kraken
{
    /// <summary>One candle from Kraken's OHLC endpoint. <paramref name="Time"/> is the candle's start in Unix seconds.</summary>
    public sealed record ApiKrakenOhlcCandle(
        long Time,
        double Open,
        double High,
        double Low,
        double Close,
        double Vwap,
        double Volume,
        int Count);
}
