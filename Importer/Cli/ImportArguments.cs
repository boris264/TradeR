using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;

namespace Importer.Cli
{
    internal enum ImportKind
    {
        Ohlcvt,
        Trades
    }

    internal sealed class ImportArguments
    {
        public const string Usage =
            "Usage:\n" +
            "  Importer ohlcvt <folder> [--parallel N]   Import Kraken OHLCVT CSVs (<PAIR>_<INTERVAL>.csv)\n" +
            "  Importer trades <folder> [--parallel N]   Import Kraken trades CSVs (<PAIR>.csv)\n" +
            "\n" +
            "Options:\n" +
            "  --parallel N   Number of files imported at the same time (default: Import:MaxParallelFiles from appsettings.json)\n" +
            "\n" +
            "Example:\n" +
            "  Importer ohlcvt \"C:\\Kraken Data\\master_q4\" --parallel 8";

        private ImportArguments(ImportKind kind, string folder, int? maxParallelFiles)
        {
            Kind = kind;
            Folder = folder;
            MaxParallelFiles = maxParallelFiles;
        }

        public ImportKind Kind { get; }

        public string Folder { get; }

        public int? MaxParallelFiles { get; }

        public static bool TryParse(
            string[] args,
            [NotNullWhen(true)] out ImportArguments? result,
            [NotNullWhen(false)] out string? error)
        {
            result = null;

            if (args.Length == 0)
            {
                error = "Missing command.";
                return false;
            }

            ImportKind kind;
            switch (args[0].ToLowerInvariant())
            {
                case "ohlcvt":
                    kind = ImportKind.Ohlcvt;
                    break;
                case "trades":
                    kind = ImportKind.Trades;
                    break;
                default:
                    error = $"Unknown command '{args[0]}'.";
                    return false;
            }

            // Options may appear before or after the folder. Every argument that isn't an option is part of
            // the folder, so an unquoted path with spaces ("C:\Kraken Data" -> "C:\Kraken", "Data") is rebuilt.
            List<string> folderParts = new List<string>();
            int? maxParallelFiles = null;

            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--parallel")
                {
                    if (i + 1 >= args.Length)
                    {
                        error = "--parallel needs a value.";
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int parallel) || parallel < 1)
                    {
                        error = $"--parallel must be a positive whole number, got '{args[i + 1]}'.";
                        return false;
                    }

                    maxParallelFiles = parallel;
                    i++;
                }
                else if (args[i].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Unknown option '{args[i]}'.";
                    return false;
                }
                else
                {
                    folderParts.Add(args[i]);
                }
            }

            string folder = NormalizeFolder(string.Join(' ', folderParts));
            if (folder.Length == 0)
            {
                error = "Missing folder.";
                return false;
            }

            result = new ImportArguments(kind, folder, maxParallelFiles);
            error = null;
            return true;
        }

        /// <summary>
        /// Removes stray quotes and trailing slashes. On Windows, <c>"C:\Kraken Data\"</c> arrives as
        /// <c>C:\Kraken Data"</c> because <c>\"</c> is read as an escaped quote.
        /// </summary>
        private static string NormalizeFolder(string folder)
        {
            string normalized = folder.Trim().Trim('"').Trim().TrimEnd('\\', '/');

            // Keep a drive root as "C:\"; a bare "C:" means the current directory on that drive.
            if (normalized.Length == 2 && normalized[1] == ':')
            {
                normalized += Path.DirectorySeparatorChar;
            }

            return normalized;
        }
    }
}
