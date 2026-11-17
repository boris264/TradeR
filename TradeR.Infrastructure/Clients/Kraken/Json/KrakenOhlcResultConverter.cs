using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradeR.Infrastructure.Models.Kraken;

namespace TradeR.Infrastructure.Clients.Kraken.Json
{
    /// <summary>
    /// Reads the <c>result</c> object of Kraken's OHLC endpoint:
    /// <c>{"XXBTZUSD": [[time, "open", "high", "low", "close", "vwap", "volume", count], ...], "last": 1763078400}</c>.
    /// The pair key differs per request, and each candle is an array of mixed types, so neither maps to a plain class.
    /// </summary>
    internal sealed class KrakenOhlcResultConverter : JsonConverter<ApiKrakenOhlcResult>
    {
        private const string LastPropertyName = "last";

        public override ApiKrakenOhlcResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            EnsureToken(ref reader, JsonTokenType.StartObject);

            string? pair = null;
            List<ApiKrakenOhlcCandle> candles = new List<ApiKrakenOhlcCandle>();
            long last = 0;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                EnsureToken(ref reader, JsonTokenType.PropertyName);
                string propertyName = reader.GetString()!;
                reader.Read();

                if (propertyName == LastPropertyName)
                {
                    last = ReadInt64(ref reader);
                }
                else
                {
                    pair = propertyName;
                    ReadCandles(ref reader, candles);
                }
            }

            EnsureToken(ref reader, JsonTokenType.EndObject);

            if (pair is null)
            {
                throw new JsonException("The OHLC result contains no candle data.");
            }

            return new ApiKrakenOhlcResult(pair, candles, last);
        }

        public override void Write(Utf8JsonWriter writer, ApiKrakenOhlcResult value, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Kraken OHLC results are only read, never written.");
        }

        private static void ReadCandles(ref Utf8JsonReader reader, List<ApiKrakenOhlcCandle> candles)
        {
            EnsureToken(ref reader, JsonTokenType.StartArray);

            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                EnsureToken(ref reader, JsonTokenType.StartArray);

                reader.Read();
                long time = ReadInt64(ref reader);
                double open = ReadDouble(ref reader);
                double high = ReadDouble(ref reader);
                double low = ReadDouble(ref reader);
                double close = ReadDouble(ref reader);
                double vwap = ReadDouble(ref reader);
                double volume = ReadDouble(ref reader);
                reader.Read();
                int count = ReadInt32(ref reader);

                // Ignore any extra fields Kraken might add to the end of a candle.
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    reader.Skip();
                }

                candles.Add(new ApiKrakenOhlcCandle(time, open, high, low, close, vwap, volume, count));
            }

            EnsureToken(ref reader, JsonTokenType.EndArray);
        }

        /// <summary>Moves to the next token and reads it as a number. Kraken sends prices and volume as strings.</summary>
        private static double ReadDouble(ref Utf8JsonReader reader)
        {
            reader.Read();

            return reader.TokenType switch
            {
                JsonTokenType.String => double.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture),
                JsonTokenType.Number => reader.GetDouble(),
                _ => throw new JsonException($"Expected a number or numeric string, found {reader.TokenType}.")
            };
        }

        private static long ReadInt64(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetInt64(),
                JsonTokenType.String => long.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture),
                _ => throw new JsonException($"Expected an integer, found {reader.TokenType}.")
            };
        }

        private static int ReadInt32(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetInt32(),
                JsonTokenType.String => int.Parse(reader.GetString()!, NumberStyles.Integer, CultureInfo.InvariantCulture),
                _ => throw new JsonException($"Expected an integer, found {reader.TokenType}.")
            };
        }

        private static void EnsureToken(ref Utf8JsonReader reader, JsonTokenType expected)
        {
            if (reader.TokenType != expected)
            {
                throw new JsonException($"Expected {expected}, found {reader.TokenType}.");
            }
        }
    }
}
