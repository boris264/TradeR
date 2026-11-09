using System;
using System.Text.Json.Serialization;

namespace TradeR.Infrastructure.Clients.Kraken.Json
{
    /// <summary>The envelope every Kraken REST response uses: <c>{"error": [...], "result": {...}}</c>.</summary>
    internal sealed class KrakenResponse<TResult>
    {
        [JsonPropertyName("error")]
        public string[] Error { get; set; } = Array.Empty<string>();

        [JsonPropertyName("result")]
        public TResult? Result { get; set; }
    }
}
