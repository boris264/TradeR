namespace TradeR.Infrastructure.Models.Kraken
{
    /// <summary>
    /// Controls how Kraken names assets and pairs in responses (the <c>assetVersion</c> query parameter).
    /// Candle values are identical either way; only the names differ.
    /// </summary>
    public enum KrakenAssetVersion
    {
        /// <summary>Kraken's internal names, e.g. <c>XXBTZUSD</c>. Sent by omitting <c>assetVersion</c>.</summary>
        Legacy = 0,

        /// <summary>Display names, e.g. <c>BTC/USD</c>. Sent as <c>assetVersion=1</c>.</summary>
        Version1 = 1
    }
}
