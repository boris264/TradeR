using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Importer.Data
{
    /// <summary>
    /// Loads one file in a single transaction: binary COPY into a temp staging table, then
    /// <c>INSERT ... SELECT ... ON CONFLICT DO NOTHING</c> into the real table. Rows already in the
    /// table are skipped instead of failing the whole COPY, so re-running an import is safe.
    /// </summary>
    internal static class StagedCopy
    {
        public const int CancellationCheckInterval = 100_000;

        public static async Task<FileImportResult> RunAsync(
            NpgsqlDataSource dataSource,
            string createStagingTableSql,
            string copySql,
            Func<NpgsqlBinaryImporter, CancellationToken, long> writeRows,
            string insertFromStagingSql,
            object insertParameters,
            CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                createStagingTableSql,
                transaction: transaction,
                cancellationToken: cancellationToken));

            long rowsRead;
            await using (NpgsqlBinaryImporter importer = await connection.BeginBinaryImportAsync(copySql, cancellationToken))
            {
                rowsRead = writeRows(importer, cancellationToken);
                await importer.CompleteAsync(cancellationToken);
            }

            int rowsInserted = await connection.ExecuteAsync(new CommandDefinition(
                insertFromStagingSql,
                insertParameters,
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return new FileImportResult(rowsRead, rowsInserted);
        }
    }
}
