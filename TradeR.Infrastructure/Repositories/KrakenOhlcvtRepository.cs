using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Db;
using TradeR.Infrastructure.Repositories.Interfaces;
using TradeR.Services.Models.Market;

namespace TradeR.Infrastructure.Repositories
{
    public class KrakenOhlcvtRepository : IKrakenOhlcvtRepository
    {
        private const string EnsureAssetPairExistsSql =
            "INSERT INTO kraken.asset_pairs (name) VALUES (@Name) ON CONFLICT DO NOTHING";

        private const string StreamAssetPairCandlesSql = """
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

        private const string GetAvailableAssetPairsSql = """
            SELECT name
            FROM kraken.asset_pairs
        """;

        private const string GetAvailableTimespanForAssetPairSql = """
            SELECT MIN(timestamp) AS from,
                   MAX(timestamp) AS to
            FROM kraken.ohlcvt
            WHERE asset_pair = @AssetPair AND 
                  interval_minutes = @Interval
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

        public async IAsyncEnumerable<KrakenOhlcvt> StreamAssetPairCandlesForTimespan(
            string assetPair,
            long from,
            long to,
            int interval,
            [EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var reader = await connection.ExecuteReaderAsync(new CommandDefinition(
                StreamAssetPairCandlesSql,
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

        public async Task<IEnumerable<KrakenAssetPair>> GetAvailableAssetPairs(CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return await connection.QueryAsync<KrakenAssetPair>(new CommandDefinition(
                GetAvailableAssetPairsSql,
                cancellationToken: cancellationToken));
        }

        public async Task<(DateTime from, DateTime to)> GetAvailableTimespanForAssetPair(
            string assetPair,
            MarketInterval interval,
            CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var timestamps = await connection.QueryFirstAsync<(long from, long to)>(new CommandDefinition(
                GetAvailableTimespanForAssetPairSql,
                new
                {
                    AssetPair = assetPair,
                    Interval = interval
                },
                cancellationToken: cancellationToken));

            return (
                DateTimeOffset.FromUnixTimeSeconds(timestamps.from).ToUniversalTime().Date,
                DateTimeOffset.FromUnixTimeSeconds(timestamps.to).ToUniversalTime().Date
            );
        }
    }
}
