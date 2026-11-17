using System;
using System.Collections.Generic;
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
        /// <returns>
        /// The candles in the period, ordered by time. The stream is empty when there are no candles in it.
        /// </returns>
        IAsyncEnumerable<OhlcvtCandle> Load(string assetPair, DateTime from, DateTime to, MarketInterval interval);
    }
}
