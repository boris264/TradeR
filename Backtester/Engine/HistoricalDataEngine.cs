using Backtester.Engine.Interfaces;
using System;
using System.Threading.Tasks;
using TradeR.Services.Market.Interfaces;
using TradeR.Services.Strategies.Interfaces;
using TradeR.SharedKernel.Results;

namespace Backtester.Engine
{
    internal class HistoricalDataEngine(
        EngineConfiguration engineConfiguration,
        IMarketDataLoader marketDataLoader,
        IStrategy strategy) : IEngine
    {
        private readonly EngineConfiguration engineConfiguration = engineConfiguration;

        private readonly IMarketDataLoader marketDataLoader = marketDataLoader;
        private readonly IStrategy strategy = strategy;

        public string AssetPair => engineConfiguration.AssetPair;

        public async Task<Result> StartAsync()
        {
            throw new NotImplementedException();
        }
    }
}
