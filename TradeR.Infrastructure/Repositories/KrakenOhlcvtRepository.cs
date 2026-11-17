using Dapper;
using Npgsql;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Db;
using TradeR.Infrastructure.Repositories.Interfaces;

namespace TradeR.Infrastructure.Repositories
{
    public class KrakenOhlcvtRepository : IKrakenOhlcvtRepository
    {
        private const string EnsureAssetPairExistsSql =
            "INSERT INTO kraken.asset_pairs (name) VALUES (@Name) ON CONFLICT DO NOTHING";

        private static readonly string GetAssetPairCandlesSql = """
            SELECT timestamp,
                   open,
                   high,
                   low,
                   close,
                   volume,
                   trades
            FROM kraken.ohlcvt
            WHERE asset_pair = @AssetPair 
                AND interval_minutes = @IntervalMinutes 
                AND timestamp >= @From
                AND timestamp < @To
            ORDER BY timestamp
        """;

        private readonly NpgsqlDataSource dataSource;

        public KrakenOhlcvtRepository(NpgsqlDataSource npgsqlDataSource)
        {
            this.dataSource = npgsqlDataSource;
        }

        public async Task EnsureAssetPairExistsAsync(string name, CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                EnsureAssetPairExistsSql,
                new { Name = name },
                cancellationToken: cancellationToken));
        }

        public async IAsyncEnumerable<KrakenOhlcvt> GetAssetPairCandlesForTimespan(
            string assetPair,
            long from,
            long to,
            int interval,
            [EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                GetAssetPairCandlesSql,
                new
                {
                    AssetPair = assetPair,
                    IntervalMinutes = interval,
                    From = from,
                    To = to
                },
                cancellationToken: cancellationToken));

            while (await reader.ReadAsync(cancellationToken))
            {
                var candle = new KrakenOhlcvt
                {
                    Timestamp = reader.GetInt64(0),
                    Open = reader.GetDouble(1),
                    High = reader.GetDouble(2),
                    Low = reader.GetDouble(3),
                    Close = reader.GetDouble(4),
                    Volume = reader.GetDouble(5),
                    Trades = reader.GetInt32(6)
                };

                yield return candle;
            }
        }
    }
}
