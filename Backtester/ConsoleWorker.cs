using Backtester.Backtester.Interfaces;
using Backtester.Configurations;
using Backtester.Engine;
using Backtester.Menu.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Services.Strategies.Interfaces;
using TradeR.SharedKernel.Results;

namespace Backtester
{
    internal class ConsoleWorker
    {
        private readonly IBacktester<EngineConfiguration> backtester;
        private readonly IMenu<IEnumerable<EngineConfiguration>> menu;
        private readonly IEnumerable<IStrategy> strategies;

        private readonly CancellationToken applicationShuttingDownToken;
        private readonly BacktestsConfiguration backtestsConfiguration;

        public ConsoleWorker(
            IBacktester<EngineConfiguration> backtester,
            IMenu<IEnumerable<EngineConfiguration>> menu,
            IHostApplicationLifetime hostApplicationLifetime,
            IOptions<BacktestsConfiguration> options,
            IEnumerable<IStrategy> strategies)
        {
            this.backtester = backtester;
            this.menu = menu;
            this.strategies = strategies;

            applicationShuttingDownToken = hostApplicationLifetime.ApplicationStopping;
            backtestsConfiguration = options.Value;
        }

        public async Task StartAsync()
        {
            IEnumerable<EngineConfiguration> engineConfigurations;

            if (backtestsConfiguration.Backtests.Count > 0)
            {
                engineConfigurations = backtestsConfiguration.Backtests
                    .Select(x => new EngineConfiguration(
                        x.AssetPair,
                        x.StrategyName,
                        x.From,
                        x.To,
                        x.Interval,
                        x.RiskSize,
                        x.InitialBalance));
            }
            else
            {
                engineConfigurations = await menu.Prompt(applicationShuttingDownToken);
            }

            await backtester.RunAsync(engineConfigurations, applicationShuttingDownToken);
        }

        private async Task<Result<EngineConfiguration>> ValidateAndParsePreconfiguredBacktests()
        {
            var strategyNames = strategies
                .Select(x => x.Name)
                .ToHashSet();
            // Check if asset pairs exists
            // Check if the selected strategy exists

            var invalidStrategies = backtestsConfiguration.Backtests
                .Where(b => !strategyNames.Contains(b.StrategyName))
                .ToList();

            if (invalidStrategies.Count > 0)
            {
                return Result.Failure<EngineConfiguration>(
                    new Error(
                        "ConsoleWorker.BacktestsConfiguration",
                        $"Invalid strategies were selected in the preconfigured backtest: {string.Join(", ", invalidStrategies)}"));
            }
            return null;
            // Check if the provided from and to for it is valid
            // Create the new configurations, capture exceptions from their constructors
            // and report the error if data is invalid
        }
    }
}
