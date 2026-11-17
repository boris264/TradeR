namespace TradeR.Services.Models.Candles
{
    public record OhlcvtCandle(
        long Timestamp,
        double Open,
        double High,
        double Low,
        double Close,
        double Volume,
        int Trades);
}
