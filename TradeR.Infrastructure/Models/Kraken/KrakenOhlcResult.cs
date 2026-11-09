using System.Collections.Generic;

namespace TradeR.Infrastructure.Models.Kraken
{
    /// <summary>Candles returned by Kraken's OHLC endpoint.</summary>
    /// <param name="Pair">
    /// The pair as Kraken names it, which depends on the requested <see cref="KrakenAssetVersion"/>:
    /// <c>BTC/USD</c> for <see cref="KrakenAssetVersion.Version1"/>, <c>XXBTZUSD</c> for <see cref="KrakenAssetVersion.Legacy"/>.
    /// </param>
    /// <param name="Candles">Candles ordered oldest to newest. The last one is still in progress and will change until its interval ends.</param>
    /// <param name="Last">Start time of the last closed candle. Pass it as <c>since</c> on the next call to get that candle again plus newer ones.</param>
    public sealed record KrakenOhlcResult(string Pair, IReadOnlyList<KrakenOhlcCandle> Candles, long Last);
}
