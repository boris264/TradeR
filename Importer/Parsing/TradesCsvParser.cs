using System;
using System.Collections.Generic;
using System.IO;
using Importer.Models;

namespace Importer.Parsing
{
    /// <summary>
    /// Reads Kraken trades files: <c>timestamp,price,volume,type,order_type,misc,trade_id</c>.
    /// <c>misc</c> is not stored; <c>trade_id</c> is always taken from the last column.
    /// </summary>
    internal static class TradesCsvParser
    {
        private const int MinColumnCount = 7;
        private const int MaxColumnCount = 16;

        // Ids seeded in kraken.trade_types and kraken.order_types.
        private const int BuyTradeTypeId = 1;
        private const int SellTradeTypeId = 2;
        private const int MarketOrderTypeId = 1;
        private const int LimitOrderTypeId = 2;

        public static IEnumerable<TradeRow> Read(string path)
        {
            string fileName = Path.GetFileName(path);

            foreach ((string line, int lineNumber) in CsvLines.Read(path))
            {
                yield return ParseLine(line, fileName, lineNumber);
            }
        }

        private static TradeRow ParseLine(string line, string fileName, int lineNumber)
        {
            ReadOnlySpan<char> span = line.AsSpan();
            Span<Range> fields = stackalloc Range[MaxColumnCount];
            int count = span.Split(fields, ',');

            if (count < MinColumnCount)
            {
                throw CsvLines.Error(fileName, lineNumber, $"expected at least {MinColumnCount} columns, found {count}");
            }

            return new TradeRow(
                TradeId: CsvLines.ParseLong(span[fields[count - 1]], "trade_id", fileName, lineNumber),
                Timestamp: CsvLines.ParseDouble(span[fields[0]], "timestamp", fileName, lineNumber),
                Price: CsvLines.ParseDouble(span[fields[1]], "price", fileName, lineNumber),
                Volume: CsvLines.ParseDouble(span[fields[2]], "volume", fileName, lineNumber),
                TradeTypeId: ParseTradeType(span[fields[3]], fileName, lineNumber),
                OrderTypeId: ParseOrderType(span[fields[4]], fileName, lineNumber));
        }

        private static int ParseTradeType(ReadOnlySpan<char> field, string fileName, int lineNumber)
        {
            ReadOnlySpan<char> value = field.Trim();

            if (value.Equals("b", StringComparison.OrdinalIgnoreCase) || value.Equals("buy", StringComparison.OrdinalIgnoreCase))
            {
                return BuyTradeTypeId;
            }

            if (value.Equals("s", StringComparison.OrdinalIgnoreCase) || value.Equals("sell", StringComparison.OrdinalIgnoreCase))
            {
                return SellTradeTypeId;
            }

            throw CsvLines.Error(fileName, lineNumber, $"unknown trade type '{field.ToString()}'");
        }

        private static int ParseOrderType(ReadOnlySpan<char> field, string fileName, int lineNumber)
        {
            ReadOnlySpan<char> value = field.Trim();

            if (value.Equals("m", StringComparison.OrdinalIgnoreCase) || value.Equals("market", StringComparison.OrdinalIgnoreCase))
            {
                return MarketOrderTypeId;
            }

            if (value.Equals("l", StringComparison.OrdinalIgnoreCase) || value.Equals("limit", StringComparison.OrdinalIgnoreCase))
            {
                return LimitOrderTypeId;
            }

            throw CsvLines.Error(fileName, lineNumber, $"unknown order type '{field.ToString()}'");
        }
    }
}
