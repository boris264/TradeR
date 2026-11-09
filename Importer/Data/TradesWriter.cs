using System.Threading;
using System.Threading.Tasks;
using Importer.Files;
using Importer.Models;
using Importer.Parsing;
using Npgsql;
using NpgsqlTypes;

namespace Importer.Data
{
    /// <summary>Imports one trades CSV into <c>kraken.time_and_sales</c>.</summary>
    internal sealed class TradesWriter : IFileImporter
    {
        private const string CreateStagingTableSql = @"
            CREATE TEMP TABLE time_and_sales_staging
            (
                trade_id      BIGINT           NOT NULL,
                timestamp     DOUBLE PRECISION NOT NULL,
                price         DOUBLE PRECISION NOT NULL,
                volume        DOUBLE PRECISION NOT NULL,
                trade_type_id INT              NOT NULL,
                order_type_id INT              NOT NULL
            ) ON COMMIT DROP";

        private const string CopySql =
            "COPY time_and_sales_staging (trade_id, timestamp, price, volume, trade_type_id, order_type_id) FROM STDIN (FORMAT BINARY)";

        private const string InsertFromStagingSql = @"
            INSERT INTO kraken.time_and_sales (asset_pair, trade_id, timestamp, price, volume, trade_type_id, order_type_id)
            SELECT @AssetPair, trade_id, timestamp, price, volume, trade_type_id, order_type_id
            FROM time_and_sales_staging
            ON CONFLICT (asset_pair, trade_id) DO NOTHING";

        private readonly NpgsqlDataSource _dataSource;

        public TradesWriter(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public Task<FileImportResult> ImportAsync(ImportFile file, CancellationToken cancellationToken)
        {
            return StagedCopy.RunAsync(
                _dataSource,
                CreateStagingTableSql,
                CopySql,
                (importer, token) => WriteRows(importer, file.Path, token),
                InsertFromStagingSql,
                new { file.AssetPair },
                cancellationToken);
        }

        private static long WriteRows(NpgsqlBinaryImporter importer, string path, CancellationToken cancellationToken)
        {
            long count = 0;

            foreach (TradeRow row in TradesCsvParser.Read(path))
            {
                importer.StartRow();
                importer.Write(row.TradeId, NpgsqlDbType.Bigint);
                importer.Write(row.Timestamp, NpgsqlDbType.Double);
                importer.Write(row.Price, NpgsqlDbType.Double);
                importer.Write(row.Volume, NpgsqlDbType.Double);
                importer.Write(row.TradeTypeId, NpgsqlDbType.Integer);
                importer.Write(row.OrderTypeId, NpgsqlDbType.Integer);

                if (++count % StagedCopy.CancellationCheckInterval == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return count;
        }
    }
}
