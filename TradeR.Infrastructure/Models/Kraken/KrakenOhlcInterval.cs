namespace TradeR.Infrastructure.Models.Kraken
{
    /// <summary>Candle intervals supported by Kraken's OHLC endpoint. Each value is the interval in minutes.</summary>
    public enum KrakenOhlcInterval
    {
        OneMinute = 1,
        FiveMinutes = 5,
        FifteenMinutes = 15,
        ThirtyMinutes = 30,
        OneHour = 60,
        FourHours = 240,
        OneDay = 1440,
        OneWeek = 10080,
        FifteenDays = 21600
    }
}
