using System;
using System.Threading;
using System.Threading.Tasks;
using Importer.Files;
using Importer.Models;
using Importer.Parsing;
using Npgsql;
using NpgsqlTypes;

namespace Importer.Data
{
    /// <summary>Imports one OHLCVT CSV into <c>kraken.ohlcvt</c>.</summary>
    internal sealed class OhlcvtWriter : IFileImporter
    {
        private const string CreateStagingTableSql = @"
            CREATE TEMP TABLE ohlcvt_staging
            (
                timestamp BIGINT           NOT NULL,
                open      DOUBLE PRECISION NOT NULL,
                high      DOUBLE PRECISION NOT NULL,
                low       DOUBLE PRECISION NOT NULL,
                close     DOUBLE PRECISION NOT NULL,
                volume    DOUBLE PRECISION NOT NULL,
                trades    INT              NOT NULL
            ) ON COMMIT DROP";

        private const string CopySql =
            "COPY ohlcvt_staging (timestamp, open, high, low, close, volume, trades) FROM STDIN (FORMAT BINARY)";

        private const string InsertFromStagingSql = @"
            INSERT INTO kraken.ohlcvt (asset_pair, interval_minutes, timestamp, open, high, low, close, volume, trades)
            SELECT @AssetPair, @IntervalMinutes, timestamp, open, high, low, close, volume, trades
            FROM ohlcvt_staging
            ON CONFLICT (asset_pair, interval_minutes, timestamp) DO NOTHING";

        private readonly NpgsqlDataSource _dataSource;

        public OhlcvtWriter(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public Task<FileImportResult> ImportAsync(ImportFile file, CancellationToken cancellationToken)
        {
            if (file.IntervalMinutes is not int intervalMinutes)
            {
                throw new ArgumentException($"{file.FileName} has no interval.", nameof(file));
            }

            return StagedCopy.RunAsync(
                _dataSource,
                CreateStagingTableSql,
                CopySql,
                (importer, token) => WriteRows(importer, file.Path, token),
                InsertFromStagingSql,
                new { file.AssetPair, IntervalMinutes = intervalMinutes },
                cancellationToken);
        }

        private static long WriteRows(NpgsqlBinaryImporter importer, string path, CancellationToken cancellationToken)
        {
            long count = 0;

            foreach (OhlcvtRow row in OhlcvtCsvParser.Read(path))
            {
                importer.StartRow();
                importer.Write(row.Timestamp, NpgsqlDbType.Bigint);
                importer.Write(row.Open, NpgsqlDbType.Double);
                importer.Write(row.High, NpgsqlDbType.Double);
                importer.Write(row.Low, NpgsqlDbType.Double);
                importer.Write(row.Close, NpgsqlDbType.Double);
                importer.Write(row.Volume, NpgsqlDbType.Double);
                importer.Write(row.Trades, NpgsqlDbType.Integer);

                if (++count % StagedCopy.CancellationCheckInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return count;
        }
    }
}
