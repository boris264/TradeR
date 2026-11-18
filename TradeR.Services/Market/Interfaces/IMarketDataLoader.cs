using System.Collections.Generic;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;

namespace TradeR.Services.Market.Interfaces
{
    public interface IMarketDataLoader
    {
        /// <summary>
        /// Loads candles for an asset, in the specified timespan for the provided interval
        /// </summary>
        /// <param name="request">Information about the wanted asset, timespan, interval and the amount of candles
        /// to preload
        /// </param>
        /// <returns>A stream of OHLCVT candles for the asset</returns>
        IAsyncEnumerable<OhlcvtCandle> Load(MarketDataLoadRequest request);
    }
}
