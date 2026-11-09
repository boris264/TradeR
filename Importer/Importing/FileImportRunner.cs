using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Importer.Data;
using Importer.Files;

namespace Importer.Importing
{
    internal sealed record ImportSummary(
        int FilesSucceeded,
        IReadOnlyCollection<(string FileName, string Error)> FailedFiles,
        long RowsInserted,
        long RowsSkipped,
        TimeSpan Elapsed);

    /// <summary>Imports files in parallel, logging progress per file and continuing past failures.</summary>
    internal sealed class FileImportRunner
    {
        private readonly AssetPairRepository _assetPairs;
        private readonly IFileImporter _fileImporter;
        private readonly int _maxParallelFiles;

        public FileImportRunner(AssetPairRepository assetPairs, IFileImporter fileImporter, int maxParallelFiles)
        {
            _assetPairs = assetPairs;
            _fileImporter = fileImporter;
            _maxParallelFiles = maxParallelFiles;
        }

        public async Task<ImportSummary> RunAsync(IReadOnlyList<ImportFile> files, CancellationToken cancellationToken)
        {
            Stopwatch total = Stopwatch.StartNew();
            ConcurrentBag<(string FileName, string Error)> failures = new ConcurrentBag<(string FileName, string Error)>();
            int processed = 0;
            int succeeded = 0;
            long rowsInserted = 0;
            long rowsSkipped = 0;

            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxParallelFiles,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(files, options, async (file, token) =>
            {
                Stopwatch elapsed = Stopwatch.StartNew();

                try
                {
                    await _assetPairs.EnsureExistsAsync(file.AssetPair, token);
                    FileImportResult result = await _fileImporter.ImportAsync(file, token);

                    Interlocked.Increment(ref succeeded);
                    Interlocked.Add(ref rowsInserted, result.RowsInserted);
                    Interlocked.Add(ref rowsSkipped, result.RowsSkipped);

                    int number = Interlocked.Increment(ref processed);
                    Console.WriteLine(
                        $"[{number}/{files.Count}] {file.FileName}: {result.RowsInserted:N0} inserted, " +
                        $"{result.RowsSkipped:N0} skipped ({elapsed.Elapsed.TotalSeconds:F1}s)");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures.Add((file.FileName, ex.Message));

                    int number = Interlocked.Increment(ref processed);
                    Console.Error.WriteLine($"[{number}/{files.Count}] {file.FileName}: FAILED - {ex.Message}");
                }
            });

            return new ImportSummary(succeeded, failures, rowsInserted, rowsSkipped, total.Elapsed);
        }
    }
}
