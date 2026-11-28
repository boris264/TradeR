using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Db;
using TradeR.Infrastructure.Models.Dtos;
using TradeR.Services.Models.Market;

namespace TradeR.Infrastructure.Repositories.Interfaces
{
    public interface IKrakenOhlcvtRepository
    {
        /// <summary>
        /// Stores the asset pair if it isn't stored yet. Does nothing if it already is.
        /// </summary>
        /// <param name="name">The asset pair name, e.g. "XBTUSD".</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        Task EnsureAssetPairExistsAsync(
            string name,
            CancellationToken cancellationToken);

        /// <summary>
        /// Streams the stored candles of one asset pair and interval, oldest first.
        /// The stream is lazy: nothing is read until enumeration starts, and candles are read one at a time.
        /// </summary>
        /// <param name="assetPair">The asset pair name, e.g. "XBTUSD".</param>
        /// <param name="from">The start of the period, in Unix seconds (inclusive).</param>
        /// <param name="to">The end of the period, in Unix seconds (exclusive).</param>
        /// <param name="interval">The candle interval, in minutes.</param>
        /// <param name="cancellationToken">
        /// Cancels the query and the read. A token passed through <c>WithCancellation</c> on the stream works too.
        /// </param>
        /// <returns>The candles in the period, ordered by timestamp. The stream is empty when there are none.</returns>
        IAsyncEnumerable<KrakenOhlcvt> StreamAssetPairCandlesForTimespan(
            string assetPair,
            long from,
            long to,
            int interval,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets every stored asset pair, in no particular order. A pair is listed even if it has no candles.
        /// </summary>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>All stored asset pairs. Empty when there are none.</returns>
        Task<IEnumerable<KrakenAssetPair>> GetAvailableAssetPairs(
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets the dates of the oldest and newest stored candle of one asset pair and interval.
        /// </summary>
        /// <param name="assetPair">The asset pair name, e.g. "XBTUSD".</param>
        /// <param name="interval">The candle interval.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>
        /// The UTC dates of the oldest and newest candle, with the time cut off (midnight). Their
        /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Unspecified"/>. <c>null</c> when the pair has no
        /// candles at that interval.
        /// </returns>
        Task<(DateTime from, DateTime to)?> GetAvailableTimespanForAssetPair(
            string assetPair,
            MarketInterval interval,
            CancellationToken cancellationToken);

        /// <summary>
        /// Finds which of the given asset pairs aren't stored. Names are compared exactly, so the comparison is
        /// case-sensitive.
        /// </summary>
        /// <param name="assetPairs">The asset pair names to check, e.g. "XBTUSD".</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>
        /// The names that don't exist, each once even if it was passed more than once. Empty when they all exist.
        /// </returns>
        Task<HashSet<string>> GetAssetPairsThatDontExist(
            IEnumerable<string> assetPairs,
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets the timestamps of the oldest and newest stored candle for each requested asset pair and interval.
        /// <para>
        /// The ranges can lag behind the stored candles. They're only updated when the time ranges are refreshed after
        /// an import, so candles imported since then aren't included.
        /// </para>
        /// </summary>
        /// <param name="assetPairs">The asset pair and interval combinations to look up.</param>
        /// <param name="cancellationToken">Cancels the call.</param>
        /// <returns>
        /// One entry per requested combination that has a range, with the timestamps in Unix seconds, in no particular
        /// order. A combination without a range is left out, and one requested twice comes back twice.
        /// </returns>
        Task<List<KrakenAssetPairTimeRange>> GetTimeRangesForAssetPairs(
            IEnumerable<KrakenAssetPairInterval> assetPairs,
            CancellationToken cancellationToken);
    }
}
