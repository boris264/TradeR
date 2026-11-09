using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Importer.Files
{
    internal sealed record DiscoveryResult(IReadOnlyList<ImportFile> Files, IReadOnlyList<string> SkippedFileNames);

    internal static class ImportFileDiscovery
    {
        private const int DailyIntervalMinutes = 1440;

        private static readonly Regex OhlcvtFileNameRegex =
            new Regex(@"^(?<pair>.+)_(?<interval>\d+)\.csv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DailyOhlcFileNameRegex =
            new Regex(@"^(?<pair>.+)_Daily_OHLC\.csv$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Finds OHLCVT files named <c>PAIR_INTERVAL.csv</c>, plus <c>PAIR_Daily_OHLC.csv</c> (daily candles).</summary>
        public static DiscoveryResult DiscoverOhlcvt(string folder)
        {
            List<ImportFile> files = new List<ImportFile>();
            List<string> skipped = new List<string>();

            foreach (string path in EnumerateCsvFiles(folder))
            {
                string fileName = Path.GetFileName(path);

                Match match = OhlcvtFileNameRegex.Match(fileName);
                if (match.Success)
                {
                    int interval = int.Parse(match.Groups["interval"].Value, CultureInfo.InvariantCulture);
                    files.Add(new ImportFile(path, match.Groups["pair"].Value, interval));
                    continue;
                }

                Match dailyMatch = DailyOhlcFileNameRegex.Match(fileName);
                if (dailyMatch.Success)
                {
                    files.Add(new ImportFile(path, dailyMatch.Groups["pair"].Value, DailyIntervalMinutes));
                    continue;
                }

                skipped.Add(fileName);
            }

            return new DiscoveryResult(files, skipped);
        }

        /// <summary>Finds trades files named <c>PAIR.csv</c>.</summary>
        public static DiscoveryResult DiscoverTrades(string folder)
        {
            List<ImportFile> files = new List<ImportFile>();

            foreach (string path in EnumerateCsvFiles(folder))
            {
                files.Add(new ImportFile(path, Path.GetFileNameWithoutExtension(path), null));
            }

            return new DiscoveryResult(files, Array.Empty<string>());
        }

        private static IEnumerable<string> EnumerateCsvFiles(string folder)
        {
            string[] paths = Directory.GetFiles(folder, "*.csv", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            return paths;
        }

    }
}
