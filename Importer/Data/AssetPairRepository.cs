using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Importer.Data
{
    internal sealed class AssetPairRepository
    {
        private const string EnsureExistsSql =
            "INSERT INTO kraken.asset_pairs (name) VALUES (@Name) ON CONFLICT DO NOTHING";

        private readonly NpgsqlDataSource _dataSource;

        public AssetPairRepository(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public async Task EnsureExistsAsync(string name, CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                EnsureExistsSql,
                new { Name = name },
                cancellationToken: cancellationToken));
        }
    }
}
