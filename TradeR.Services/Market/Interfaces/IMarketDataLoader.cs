using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Results;

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
        Task<Result<IAsyncEnumerable<OhlcvtCandle>>> Load(MarketDataLoadRequest request, CancellationToken cancellationToken);
    }
}
