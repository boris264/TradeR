namespace TradeR.Infrastructure.Configurations.Kraken
{
    /// <summary>Settings for Kraken's REST API, bound from the <c>Kraken</c> configuration section.</summary>
    public sealed class KrakenRESTApiConfiguration
    {
        public const string SectionName = "Kraken";

        public string BaseUrl { get; set; } = "https://api.kraken.com/";
    }
}
