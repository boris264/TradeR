using System.Collections.Generic;

namespace TradeR.Infrastructure.Models.Kraken
{
    /// <summary>Candles returned by Kraken's OHLC endpoint.</summary>
    /// <param name="Pair">Kraken's internal pair name, e.g. <c>XXBTZUSD</c> when <c>XBTUSD</c> was requested.</param>
    /// <param name="Candles">Candles ordered oldest to newest. The last one is still in progress and will change until its interval ends.</param>
    /// <param name="Last">Start time of the last closed candle. Pass it as <c>since</c> on the next call to get that candle again plus newer ones.</param>
    public sealed record KrakenOhlcResult(string Pair, IReadOnlyList<KrakenOhlcCandle> Candles, long Last);
}
