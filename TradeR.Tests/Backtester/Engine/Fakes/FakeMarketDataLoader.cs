using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Services.Market.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Results;

namespace TradeR.Tests.Backtester.Engine.Fakes
{
    internal sealed class FakeMarketDataLoader : IMarketDataLoader
    {
        private readonly IReadOnlyList<OhlcvtCandle> candles;
        private readonly Error? error;

        public FakeMarketDataLoader(IEnumerable<OhlcvtCandle> candles)
        {
            this.candles = candles.ToList();
        }

        public FakeMarketDataLoader(Error error)
        {
            candles = new List<OhlcvtCandle>();
            this.error = error;
        }

        public MarketDataLoadRequest? ReceivedRequest { get; private set; }

        public Task<Result<IAsyncEnumerable<OhlcvtCandle>>> Load(
            MarketDataLoadRequest request,
            CancellationToken cancellationToken)
        {
            ReceivedRequest = request;

            if (error != null)
            {
                return Task.FromResult(Result.Failure<IAsyncEnumerable<OhlcvtCandle>>(error));
            }

            return Task.FromResult(Result.Success(Stream(candles, cancellationToken)));
        }

        private static async IAsyncEnumerable<OhlcvtCandle> Stream(
            IReadOnlyList<OhlcvtCandle> candles,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (OhlcvtCandle candle in candles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();

                yield return candle;
            }
        }
    }
}
