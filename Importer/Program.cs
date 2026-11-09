using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Importer.Cli;
using Importer.Data;
using Importer.Files;
using Importer.Importing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Importer
{
    internal class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitFilesFailed = 1;
        private const int ExitUsageError = 2;
        private const int DefaultMaxParallelFiles = 4;

        static async Task<int> Main(string[] args)
        {
            if (!ImportArguments.TryParse(args, out ImportArguments? arguments, out string? error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ImportArguments.Usage);
                return ExitUsageError;
            }

            if (!Directory.Exists(arguments.Folder))
            {
                Console.Error.WriteLine($"Folder not found: {arguments.Folder}");
                return ExitUsageError;
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddUserSecrets<Program>(optional: false)
                .AddEnvironmentVariables()
                .Build();

            string? connectionString = configuration.GetConnectionString("KrakenMarketData");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("Connection string 'KrakenMarketData' is not set. Set it with:");
                Console.Error.WriteLine("  dotnet user-secrets set ConnectionStrings:KrakenMarketData \"Host=...;Database=...;Username=...;Password=...\" --project Importer");
                return ExitUsageError;
            }

            int maxParallelFiles = arguments.MaxParallelFiles ?? ReadMaxParallelFiles(configuration);

            DiscoveryResult discovery = arguments.Kind == ImportKind.Ohlcvt
                ? ImportFileDiscovery.DiscoverOhlcvt(arguments.Folder)
                : ImportFileDiscovery.DiscoverTrades(arguments.Folder);

            foreach (string skipped in discovery.SkippedFileNames)
            {
                Console.WriteLine($"Skipping {skipped}: file name doesn't match the expected pattern.");
            }

            if (discovery.Files.Count == 0)
            {
                Console.WriteLine("No files to import.");
                return ExitSuccess;
            }

            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.Error.WriteLine("Cancelling... files in progress will be rolled back.");
                cancellation.Cancel();
            };

            NpgsqlDataSourceBuilder dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = 0; // large files take longer than the 30s default
            await using NpgsqlDataSource dataSource = dataSourceBuilder.Build();

            IFileImporter fileImporter = arguments.Kind == ImportKind.Ohlcvt
                ? new OhlcvtWriter(dataSource)
                : new TradesWriter(dataSource);

            FileImportRunner runner = new FileImportRunner(new AssetPairRepository(dataSource), fileImporter, maxParallelFiles);

            Console.WriteLine($"Importing {discovery.Files.Count:N0} {arguments.Kind} file(s) from {arguments.Folder} ({maxParallelFiles} at a time)");

            ImportSummary summary;
            try
            {
                summary = await runner.RunAsync(discovery.Files, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Import cancelled. Finished files were kept; re-run to continue.");
                return ExitFilesFailed;
            }

            Console.WriteLine();
            Console.WriteLine(
                $"Done in {summary.Elapsed:hh\\:mm\\:ss}: {summary.FilesSucceeded:N0} file(s) imported, " +
                $"{summary.FailedFiles.Count:N0} failed, {summary.RowsInserted:N0} rows inserted, {summary.RowsSkipped:N0} skipped.");

            if (summary.FailedFiles.Count > 0)
            {
                Console.Error.WriteLine("Failed files:");
                foreach ((string fileName, string fileError) in summary.FailedFiles)
                {
                    Console.Error.WriteLine($"  {fileName}: {fileError}");
                }

                return ExitFilesFailed;
            }

            return ExitSuccess;
        }

        private static int ReadMaxParallelFiles(IConfiguration configuration)
        {
            string? value = configuration["Import:MaxParallelFiles"];
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parallel) && parallel > 0
                ? parallel
                : DefaultMaxParallelFiles;
        }
    }
}
