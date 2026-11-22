using Backtester.Backtester.Interfaces;
using Backtester.Engine;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Interfaces;
using TradeR.Infrastructure.Models.Excel;
using TradeR.Services.Strategies.Interfaces;

namespace Backtester.Backtester
{
    internal class HistoricalDataBacktester : IBacktester
    {
        private readonly ITradeWriter tradeWriter;
        private readonly ILogger<HistoricalDataBacktester> logger;
        private readonly IEnumerable<IStrategy> strategies;
        private readonly IServiceProvider serviceProvider;

        public HistoricalDataBacktester(
            ITradeWriter tradeWriter,
            ILogger<HistoricalDataBacktester> logger,
            IEnumerable<IStrategy> strategies,
            IServiceProvider serviceProvider)
        {
            this.tradeWriter = tradeWriter;
            this.logger = logger;
            this.strategies = strategies;
            this.serviceProvider = serviceProvider;
        }

        public async Task RunAsync(
            IEnumerable<EngineConfiguration> engineConfigurations,
            CancellationToken cancellationToken)
        {
            try
            {
                var producedTrades = new ConcurrentBag<TradeRow>();
                await Parallel.ForEachAsync(engineConfigurations, cancellationToken, async (e, c) =>
                {
                    var selectedStrategy = strategies.FirstOrDefault(s => s.Name == e.StrategyName);

                    if (selectedStrategy == null)
                    {
                        return;
                    }

                    var engine = new HistoricalDataEngine(
                        e,
                        selectedStrategy,
                        serviceProvider);

                    var result = await engine.StartAsync(c);

                    if (result.IsSuccess)
                    {
                        result.Value.ToList().ForEach(producedTrades.Add);
                    }
                });

                var saveResult = await tradeWriter.SaveTrades($"Backtest-{DateTime.Now}.xlsx", producedTrades);

                if (saveResult.IsFailure)
                {
                    logger.LogCritical("Failed to save trades to Excel workbook, error: {Error}", saveResult.Error);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Backtests were cancelled by the user.");
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "An error has occurred during the backtest");
            }
        }
    }
}
