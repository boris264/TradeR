namespace TradeR.Services.Models.Candles
{
    public record OhlcvtCandle(
        double Open,
        double High,
        double Low,
        double Close,
        double Volume,
        double Trades);
}
