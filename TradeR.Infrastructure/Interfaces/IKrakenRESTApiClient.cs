using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Kraken;
using TradeR.SharedKernel.Results;

namespace TradeR.Infrastructure.Interfaces
{
    public interface IKrakenRESTApiClient
    {
        /// <summary>
        /// Gets candles from Kraken's public OHLC endpoint (<c>GET /0/public/OHLC</c>).
        /// Kraken only returns the 720 most recent candles for the interval, plus the current one that hasn't closed yet
        /// (always the last in the list). <paramref name="since"/> filters within that window and cannot reach further back;
        /// use the CSV history for older data.
        /// </summary>
        /// <param name="pair">Asset pair, e.g. <c>XBTUSD</c>. One pair per call.</param>
        /// <param name="interval">Candle interval.</param>
        /// <param name="since">
        /// Optional Unix time in seconds; candles starting at or after it are returned. Passing
        /// <see cref="ApiKrakenOhlcResult.Last"/> from a previous call returns the last closed candle again plus any newer ones.
        /// </param>
        /// <param name="assetVersion">
        /// How the pair is named in <see cref="ApiKrakenOhlcResult.Pair"/>: <c>BTC/USD</c> for <see cref="ApiKrakenAssetVersion.Version1"/>
        /// (default), <c>XXBTZUSD</c> for <see cref="ApiKrakenAssetVersion.Legacy"/>.
        /// </param>
        /// <param name="cancellationToken">Cancels the request. Cancellation is thrown, not returned as a failure.</param>
        /// <returns>The candles, or a failure with a <c>Kraken.*</c> error code (see <see cref="Errors.KrakenErrors"/>).</returns>
        Task<Result<ApiKrakenOhlcResult>> GetOhlcAsync(
            string pair,
            ApiKrakenOhlcInterval interval,
            long? since = null,
            ApiKrakenAssetVersion assetVersion = ApiKrakenAssetVersion.Version1,
            CancellationToken cancellationToken = default);
    }
}
