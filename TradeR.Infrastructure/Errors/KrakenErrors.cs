using System.Collections.Generic;
using System.Net;
using TradeR.SharedKernel.Results;

namespace TradeR.Infrastructure.Errors
{
    /// <summary>Errors returned by the Kraken REST API client. Codes are prefixed with <c>Kraken.</c>.</summary>
    public static class KrakenErrors
    {
        /// <summary>Kraken answered with errors such as <c>EQuery:Unknown asset pair</c>.</summary>
        public static Error Api(IReadOnlyList<string> errors)
        {
            string first = errors.Count > 0 ? errors[0] : string.Empty;
            int separator = first.IndexOf(':');
            string category = separator > 0 ? first.Substring(0, separator) : "Unknown";

            List<string> messages = new List<string>(errors.Count);
            foreach (string error in errors)
            {
                int index = error.IndexOf(':');
                messages.Add(index > 0 ? error.Substring(index + 1) : error);
            }

            return new Error($"Kraken.{category}", string.Join("; ", messages));
        }

        public static Error Http(HttpStatusCode statusCode)
        {
            return new Error("Kraken.Http", $"Kraken returned HTTP {(int)statusCode} ({statusCode}).");
        }

        public static Error Unavailable(string message)
        {
            return new Error("Kraken.Unavailable", message);
        }

        public static Error InvalidResponse(string message)
        {
            return new Error("Kraken.InvalidResponse", message);
        }

        public static Error NoData(string pair)
        {
            return new Error("Kraken.NoData", $"Kraken returned no candle data for '{pair}'.");
        }
    }
}
