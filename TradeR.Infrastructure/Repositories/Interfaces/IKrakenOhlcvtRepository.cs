using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Kraken;

namespace TradeR.Infrastructure.Repositories.Interfaces
{
    public interface IKrakenOhlcvtRepository
    {
        /// <summary>
        /// Adds the asset pair to <c>kraken.asset_pairs</c> if it isn't there yet. Does nothing if it already exists.
        /// </summary>
        /// <param name="name">The asset pair name, e.g. "XBTUSD".</param>
        /// <param name="cancellationToken">Cancels the database call.</param>
        Task EnsureAssetPairExistsAsync(
            string name,
            CancellationToken cancellationToken);

        /// <summary>
        /// Streams the stored candles of one asset pair and interval from <c>kraken.ohlcvt</c>, oldest first.
        /// The stream is lazy: the database is only queried once enumeration starts, and rows are read one at a time.
        /// </summary>
        /// <param name="assetPair">The asset pair name, e.g. "XBTUSD".</param>
        /// <param name="from">The start of the period, in Unix seconds (inclusive).</param>
        /// <param name="to">The end of the period, in Unix seconds (exclusive).</param>
        /// <param name="interval">The candle interval, in minutes.</param>
        /// <param name="cancellationToken">
        /// Cancels the query and the read. A token passed through <c>WithCancellation</c> on the stream works too.
        /// </param>
        /// <returns>The candles in the period, ordered by timestamp. The stream is empty when there are none.</returns>
        IAsyncEnumerable<KrakenOhlcvt> GetAssetPairCandlesForTimespan(
            string assetPair,
            long from,
            long to,
            int interval,
            CancellationToken cancellationToken);
    }
}
