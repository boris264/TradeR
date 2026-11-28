using Backtester.Backtester.Interfaces;
using Backtester.Configurations;
using Backtester.Engine;
using Backtester.Menu.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Models.Dtos;
using TradeR.Infrastructure.Repositories.Interfaces;
using TradeR.Services.Strategies.Interfaces;
using TradeR.SharedKernel.Results;

namespace Backtester
{
    internal class ConsoleWorker
    {
        private readonly IBacktester<EngineConfiguration> backtester;
        private readonly IMenu<IEnumerable<EngineConfiguration>> menu;
        private readonly IEnumerable<IStrategy> strategies;
        private readonly IKrakenOhlcvtRepository krakenOhlcvtRepository;
        private readonly ILogger<ConsoleWorker> logger;

        private readonly CancellationToken applicationShuttingDownToken;
        private readonly BacktestsConfiguration backtestsConfiguration;

        public ConsoleWorker(
            IBacktester<EngineConfiguration> backtester,
            IMenu<IEnumerable<EngineConfiguration>> menu,
            IHostApplicationLifetime hostApplicationLifetime,
            IOptions<BacktestsConfiguration> options,
            IEnumerable<IStrategy> strategies,
            IKrakenOhlcvtRepository krakenOhlcvtRepository,
            ILogger<ConsoleWorker> logger)
        {
            this.backtester = backtester;
            this.menu = menu;
            this.strategies = strategies;
            this.krakenOhlcvtRepository = krakenOhlcvtRepository;
            this.logger = logger;

            applicationShuttingDownToken = hostApplicationLifetime.ApplicationStopping;
            backtestsConfiguration = options.Value;
        }

        public async Task StartAsync()
        {
            IEnumerable<EngineConfiguration> engineConfigurations;

            if (backtestsConfiguration.Configurations.Count > 0)
            {
                logger.LogInformation(
                    "Found {} backtests in the configuration file, processing...",
                    backtestsConfiguration.Configurations.Count);

                var result = await ValidateAndParsePreconfiguredBacktests();

                if (result.IsFailure)
                {
                    logger.LogError(
                        "An error occurred while parsing existing backtests configurations: {Error}",
                        result.Error);

                    return;
                }

                engineConfigurations = result.Value;
            }
            else
            {
                logger.LogInformation(
                    "No backtests found in the configuration file, starting the menu");

                engineConfigurations = await menu.Prompt(applicationShuttingDownToken);
            }

            await backtester.RunAsync(engineConfigurations, applicationShuttingDownToken);
        }

        private async Task<Result<IEnumerable<EngineConfiguration>>> ValidateAndParsePreconfiguredBacktests()
        {
            var engineConfigurations = new List<EngineConfiguration>(backtestsConfiguration.Configurations.Count);

            var strategyNames = strategies
                .Select(x => x.Name)
                .ToHashSet();
            var allAssetPairs = backtestsConfiguration.Configurations
                .Select(x => x.AssetPair);
            var nonExistingAssetPairs = await krakenOhlcvtRepository.GetAssetPairsThatDontExist(
                allAssetPairs,
                applicationShuttingDownToken);
            var assetPairsTimeRanges = (await krakenOhlcvtRepository.GetTimeRangesForAssetPairs(
                backtestsConfiguration.Configurations.Select(x => new KrakenAssetPairInterval
                {
                    AssetPair = x.AssetPair,
                    IntervalMinutes = (int)x.Interval
                }),
                applicationShuttingDownToken))
                .ToDictionary(k => k.AssetPair, v => v);

            foreach (var config in backtestsConfiguration.Configurations)
            {
                if (!strategyNames.Contains(config.StrategyName))
                {
                    return Result.Failure<IEnumerable<EngineConfiguration>>(
                        new Error(
                            "ConsoleWorker.ConfigurationsConfiguration",
                            $"Invalid strategy was selected in the preconfigured backtest: {config.StrategyName}"));
                }

                if (nonExistingAssetPairs.Contains(config.AssetPair))
                {
                    return Result.Failure<IEnumerable<EngineConfiguration>>(
                        new Error(
                            "ConsoleWorker.ConfigurationsConfiguration",
                            $"Non existing asset pair was selected: {config.AssetPair}"));
                }

                var assetPairTimeRange = assetPairsTimeRanges[config.AssetPair];
                var oldestTimeUtc = DateTimeOffset
                    .FromUnixTimeSeconds(assetPairTimeRange.OldestTime)
                    .ToUniversalTime();
                var newestTimeUtc = DateTimeOffset
                    .FromUnixTimeSeconds(assetPairTimeRange.NewestTime)
                    .ToUniversalTime();

                if (config.From < oldestTimeUtc || config.To > newestTimeUtc)
                {
                    return Result.Failure<IEnumerable<EngineConfiguration>>(
                        new Error(
                            "ConsoleWorker.ConfigurationsConfiguration",
                            $"Selected From or To values were invalid, oldest available time: {oldestTimeUtc}, From: {config.From}, newest available time: {newestTimeUtc}, To: {config.To}"));
                }

                try
                {
                    var engineConfiguration = new EngineConfiguration(
                        config.AssetPair,
                        config.StrategyName,
                        DateTime.SpecifyKind(config.From, DateTimeKind.Utc),
                        DateTime.SpecifyKind(config.To, DateTimeKind.Utc),
                        config.Interval,
                        config.RiskSize,
                        config.InitialBalance);
                    engineConfigurations.Add(engineConfiguration);
                }
                catch (Exception e)
                {
                    return Result.Failure<IEnumerable<EngineConfiguration>>(
                        new Error(
                            "ConsoleWorker.ConfigurationsConfiguration",
                            $"Exception occurred while creating EngineConfiguration, message: {e.Message}"));
                }
            }

            return Result.Success<IEnumerable<EngineConfiguration>>(engineConfigurations);
        }
    }
}
