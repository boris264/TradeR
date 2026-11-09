using System;
using System.Collections.Generic;
using System.IO;
using Importer.Models;

namespace Importer.Parsing
{
    /// <summary>Reads Kraken OHLCVT files: <c>timestamp,open,high,low,close,volume,trades</c>.</summary>
    internal static class OhlcvtCsvParser
    {
        private const int ColumnCount = 7;

        public static IEnumerable<OhlcvtRow> Read(string path)
        {
            string fileName = Path.GetFileName(path);

            foreach ((string line, int lineNumber) in CsvLines.Read(path))
            {
                yield return ParseLine(line, fileName, lineNumber);
            }
        }

        private static OhlcvtRow ParseLine(string line, string fileName, int lineNumber)
        {
            ReadOnlySpan<char> span = line.AsSpan();
            Span<Range> fields = stackalloc Range[ColumnCount + 1];
            int count = span.Split(fields, ',');

            if (count != ColumnCount)
            {
                throw CsvLines.Error(fileName, lineNumber, $"expected {ColumnCount} columns, found {count}");
            }

            return new OhlcvtRow(
                Timestamp: CsvLines.ParseLong(span[fields[0]], "timestamp", fileName, lineNumber),
                Open: CsvLines.ParseDouble(span[fields[1]], "open", fileName, lineNumber),
                High: CsvLines.ParseDouble(span[fields[2]], "high", fileName, lineNumber),
                Low: CsvLines.ParseDouble(span[fields[3]], "low", fileName, lineNumber),
                Close: CsvLines.ParseDouble(span[fields[4]], "close", fileName, lineNumber),
                Volume: CsvLines.ParseDouble(span[fields[5]], "volume", fileName, lineNumber),
                Trades: CsvLines.ParseInt(span[fields[6]], "trades", fileName, lineNumber));
        }
    }
}
