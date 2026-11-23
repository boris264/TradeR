using Backtester.Backtester.Interfaces;
using Backtester.Engine;
using Backtester.Menu.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        private readonly CancellationToken applicationStoppingToken;

        private readonly ITradeWriter tradeWriter;
        private readonly ILogger<HistoricalDataBacktester> logger;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly IMenu<IEnumerable<EngineConfiguration>> menu;

        public HistoricalDataBacktester(
            ITradeWriter tradeWriter,
            ILogger<HistoricalDataBacktester> logger,
            IServiceScopeFactory serviceScopeFactory,
            IHostApplicationLifetime hostApplicationLifetime,
            IMenu<IEnumerable<EngineConfiguration>> menu)
        {
            this.tradeWriter = tradeWriter;
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
            this.menu = menu;

            applicationStoppingToken = hostApplicationLifetime.ApplicationStopping;
        }

        public async Task RunAsync()
        {
            try
            {
                var engineConfigurations = await menu.Prompt(applicationStoppingToken);

                var producedTrades = new ConcurrentBag<TradeRow>();
                await Parallel.ForEachAsync(engineConfigurations, applicationStoppingToken, async (e, c) =>
                {
                    try
                    {
                        using var scope = serviceScopeFactory.CreateScope();

                        var selectedStrategy = scope.ServiceProvider
                            .GetServices<IStrategy>()
                            .FirstOrDefault(s => s.Name == e.StrategyName);

                        if (selectedStrategy == null)
                        {
                            logger.LogWarning("No strategy named {StrategyName} was found, skipping backtest {Backtest}", e.StrategyName, e);
                            return;
                        }

                        var engine = new HistoricalDataEngine(
                            e,
                            selectedStrategy,
                            scope);

                        var result = await engine.StartAsync(c);

                        if (result.IsSuccess)
                        {
                            result.Value.ToList().ForEach(producedTrades.Add);
                        }
                        else
                        {
                            logger.LogError(
                                "An error occurred with backtest with the parameters {Backtest}, message: {Error}",
                                e,
                                result.Error);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !c.IsCancellationRequested)
                    {
                        logger.LogError(ex, "Backtest {Backtest} failed, the other backtests continue", e);
                    }
                });

                if (producedTrades.Count == 0)
                {
                    logger.LogInformation("No trades were generated from the selected backtests.");
                    return;
                }

                var saveResult = await tradeWriter.SaveTrades($"Backtest-{DateTime.Now:HH_mm_dd-MM-yyyy}.xlsx", producedTrades);

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
