using System;
using System.Collections.Generic;
using System.Threading;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;

namespace TradeR.Services.Interfaces
{
    public interface ICandleReader
    {
        /// <summary>
        /// Streams the stored OHLCVT candles of one asset pair and interval, oldest first.
        /// Candles are read one at a time and never loaded into memory all at once.
        /// </summary>
        /// <param name="assetPair">The asset pair, e.g. "BTC/USD".</param>
        /// <param name="from">The start of the period, in UTC (inclusive).</param>
        /// <param name="to">The end of the period, in UTC (exclusive).</param>
        /// <param name="interval">The candle interval to read.</param>
        /// <param name="cancellationToken">Cancels reading the stream.</param>
        /// <returns>
        /// The candles in the period, ordered by time. The stream is empty when there are no candles in it.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="assetPair"/> is empty or whitespace, or <paramref name="from"/> or <paramref name="to"/>
        /// doesn't have <see cref="DateTimeKind.Utc"/>, or <paramref name="from"/> is after <paramref name="to"/>.
        /// Thrown as soon as <c>Load</c> is called, not when the stream is enumerated.
        /// </exception>
        IAsyncEnumerable<OhlcvtCandle> StreamCandles(
            string assetPair,
            DateTime from,
            DateTime to,
            MarketInterval interval,
            CancellationToken cancellationToken);
    }
}
