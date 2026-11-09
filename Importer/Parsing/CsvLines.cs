using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Importer.Parsing
{
    /// <summary>Shared helpers for reading Kraken CSV files line by line.</summary>
    internal static class CsvLines
    {
        private const int BufferSize = 1 << 16;

        /// <summary>
        /// Yields the non-empty lines of a file with their 1-based line numbers.
        /// A first line that doesn't start with a digit is treated as a header and skipped.
        /// </summary>
        public static IEnumerable<(string Line, int LineNumber)> Read(string path)
        {
            FileStreamOptions options = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = BufferSize,
                Options = FileOptions.SequentialScan
            };

            using StreamReader reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, options);

            int lineNumber = 0;
            bool firstDataLineSeen = false;
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!firstDataLineSeen)
                {
                    firstDataLineSeen = true;
                    if (!char.IsAsciiDigit(line.TrimStart()[0]))
                    {
                        continue;
                    }
                }

                yield return (line, lineNumber);
            }
        }

        public static long ParseLong(ReadOnlySpan<char> field, string column, string fileName, int lineNumber)
        {
            if (!long.TryParse(field.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                throw Error(fileName, lineNumber, $"invalid {column} '{field.ToString()}'");
            }

            return value;
        }

        public static int ParseInt(ReadOnlySpan<char> field, string column, string fileName, int lineNumber)
        {
            if (!int.TryParse(field.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw Error(fileName, lineNumber, $"invalid {column} '{field.ToString()}'");
            }

            return value;
        }

        /// <summary>Parses a decimal number, including scientific notation such as <c>4.933e-05</c>.</summary>
        public static double ParseDouble(ReadOnlySpan<char> field, string column, string fileName, int lineNumber)
        {
            if (!double.TryParse(field.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                throw Error(fileName, lineNumber, $"invalid {column} '{field.ToString()}'");
            }

            return value;
        }

        public static FormatException Error(string fileName, int lineNumber, string message)
        {
            return new FormatException($"{fileName} line {lineNumber}: {message}");
        }
    }
}
