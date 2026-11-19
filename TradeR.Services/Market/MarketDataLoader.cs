using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Services.Interfaces;
using TradeR.Services.Market.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.Services.Validators;
using TradeR.SharedKernel.Results;

namespace TradeR.Services.Market
{
    public class MarketDataLoader : IMarketDataLoader
    {
        private readonly ICandleReader candleReader;

        public MarketDataLoader(
            ICandleReader candleReader)
        {
            this.candleReader = candleReader;
        }

        public async Task<Result<IAsyncEnumerable<OhlcvtCandle>>> Load(
            MarketDataLoadRequest request,
            CancellationToken cancellationToken)
        {
            var requestValidationResult = request.Validate();

            if (requestValidationResult.IsFailure)
            {
                return Result.Failure<IAsyncEnumerable<OhlcvtCandle>>(requestValidationResult.Error);
            }
            var candlesStream = candleReader.StreamCandles(
                request.AssetPair,
                request.From,
                request.To,
                request.Interval,
                cancellationToken);

            if (request.CandlesToPreload > 0)
            {
                var preloadCandlesStartTimespan = request.From.AddMinutes(-((int)request.Interval * request.CandlesToPreload));

                var candlesForPreloadStream = candleReader.StreamCandles(
                    request.AssetPair,
                    preloadCandlesStartTimespan,
                    request.From,
                    request.Interval,
                    cancellationToken);

                candlesStream = AsyncEnumerable.Concat(candlesForPreloadStream, candlesStream);
            }

            return Result.Success(candlesStream);
        }
    }
}
