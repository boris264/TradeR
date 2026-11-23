using Backtester.Engine;
using Backtester.Menu.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.Repositories.Interfaces;
using TradeR.Services.Models.Market;
using TradeR.Services.Strategies.Interfaces;

namespace Backtester.Menu
{
    internal class BacktestingMenu : IMenu<IEnumerable<EngineConfiguration>>
    {
        private const string menuHeader = """
  |                 _____                   _        ____
 _|_     |         |_   _| _ __   __ _   __| |  ___ |  _ \
|   |   _|_    |     | |  | '__| / _` | / _` | / _ \| |_) |
|___|  |   |  _|_    | |  | |   | (_| || (_| ||  __/|  _ <
  |    |___| |   |   |_|  |_|    \__,_| \__,_| \___||_| \_\
         |   |___|
               |
        Crypto strategy backtester

""";
        private const string mainMenuOptionsTemplate = """
==========================================
{0}
==========================================
""";


        private readonly IKrakenOhlcvtRepository krakenOhlcvtRepository;
        private readonly IEnumerable<IStrategy> strategies;

        public BacktestingMenu(
            IKrakenOhlcvtRepository krakenOhlcvtRepository,
            IEnumerable<IStrategy> strategies)
        {
            this.krakenOhlcvtRepository = krakenOhlcvtRepository;
            this.strategies = strategies;
        }

        public async Task<IEnumerable<EngineConfiguration>> Prompt(CancellationToken cancellationToken)
        {
            Console.Write(menuHeader);
            Console.WriteLine(string.Format(mainMenuOptionsTemplate, BuildOptions("Exit", "Run Backtest")));

            var choice = ParseChoice();

            switch (choice)
            {
                case MenuOption.Run:
                    return await ParseBacktestConfigurations(cancellationToken);

                case MenuOption.Exit:
                default:
                    Console.WriteLine("Exiting...");

                    return [];
            }
        }

        private async Task<IEnumerable<EngineConfiguration>> ParseBacktestConfigurations(CancellationToken cancellationToken)
        {
            var configurations = new List<EngineConfiguration>();

            var availableAssetPairs = await krakenOhlcvtRepository.GetAvailableAssetPairs(cancellationToken);
            var assetPairNames = availableAssetPairs
                .Select(x => x.Name)
                .ToList();
            var strategyNames = strategies
                .Select(x => x.Name)
                .ToList();
            var intervals = Enum.GetValues<MarketInterval>();

            while (true)
            {
                Console.Clear();
                Console.WriteLine(menuHeader);

                if (configurations.Count > 0)
                {
                    Console.WriteLine("Added backtests:");
                    foreach (var configuration in configurations)
                    {
                        Console.WriteLine(
                            $"  {configuration.StrategyName} | {configuration.AssetPair} | {configuration.Interval} | " +
                            $"{configuration.From:yyyy-MM-dd} to {configuration.To:yyyy-MM-dd} | " +
                            $"balance {configuration.InitialBalance.ToString(CultureInfo.InvariantCulture)} | " +
                            $"risk {(configuration.RiskSize * 100).ToString(CultureInfo.InvariantCulture)}%");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("Select an asset pair:");
                Console.WriteLine(string.Format(mainMenuOptionsTemplate, BuildOptions("Finish", assetPairNames)));

                var assetPairIndex = ReadOptionIndex(assetPairNames.Count, assetPairNames);
                if (assetPairIndex == 0)
                {
                    return configurations;
                }

                var assetPair = assetPairNames[assetPairIndex - 1];

                Console.WriteLine("Select a strategy:");
                Console.WriteLine(string.Format(mainMenuOptionsTemplate, BuildOptions("Back to asset pairs", strategyNames)));

                var strategyIndex = ReadOptionIndex(strategyNames.Count);
                if (strategyIndex == 0)
                {
                    continue;
                }

                var strategyName = strategyNames[strategyIndex - 1];

                Console.WriteLine("Select an interval:");
                Console.WriteLine(string.Format(
                    mainMenuOptionsTemplate,
                    BuildOptions("Back to asset pairs", intervals.Select(x => x.ToString()))));

                var intervalIndex = ReadOptionIndex(intervals.Length);
                if (intervalIndex == 0)
                {
                    continue;
                }

                var interval = intervals[intervalIndex - 1];

                var availableTimespan = await krakenOhlcvtRepository.GetAvailableTimespanForAssetPair(
                    assetPair,
                    interval,
                    cancellationToken);
                var firstAvailableDate = DateTime.SpecifyKind(availableTimespan.from, DateTimeKind.Utc);
                var lastAvailableDate = DateTime.SpecifyKind(availableTimespan.to, DateTimeKind.Utc);
                var endOfAvailableData = lastAvailableDate.AddDays(1);

                Console.WriteLine($"Available data: {firstAvailableDate:yyyy-MM-dd} to {lastAvailableDate:yyyy-MM-dd}");

                var from = ReadDate("From", firstAvailableDate, firstAvailableDate, lastAvailableDate);
                var to = ReadDate("To (exclusive)", endOfAvailableData, from.AddDays(1), endOfAvailableData);

                var initialBalance = ReadDouble("Initial account balance", 5000.0, x => x > 0);
                var riskPercent = ReadDouble("Risk per trade (%)", null, x => x > 0 && x <= 100);

                var newConfiguration = new EngineConfiguration(
                    assetPair,
                    strategyName,
                    from,
                    to,
                    interval,
                    riskPercent / 100,
                    initialBalance);

                if (configurations.Contains(newConfiguration))
                {
                    Console.WriteLine($"Backtest with these parameters already exists!");
                }
                else
                {
                    configurations.Add(newConfiguration);
                }
            }
        }

        private MenuOption ParseChoice()
        {
            var choiceString = string.Empty;
            var choice = MenuOption.Exit;

            Console.Write("Select a choice: ");
            choiceString = Console.ReadLine();

            while (!Enum.TryParse(choiceString, out choice))
            {
                Console.WriteLine("Invalid choice, please try again!");
                Console.Write("Select a choice: ");
                choiceString = Console.ReadLine();
            }

            return choice;
        }

        private int ReadOptionIndex(int optionCount, IReadOnlyList<string>? optionNames = null)
        {
            var prompt = optionNames != null
                ? "Select a choice (number or name): "
                : "Select a choice: ";

            while (true)
            {
                Console.Write(prompt);
                var input = Console.ReadLine()?.Trim();

                if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    && index >= 0
                    && index <= optionCount)
                {
                    return index;
                }

                if (optionNames != null && !string.IsNullOrEmpty(input))
                {
                    for (var i = 0; i < optionNames.Count; i++)
                    {
                        if (string.Equals(optionNames[i], input, StringComparison.OrdinalIgnoreCase))
                        {
                            return i + 1;
                        }
                    }
                }

                Console.WriteLine("Invalid choice, please try again!");
            }
        }

        private DateTime ReadDate(string label, DateTime defaultValue, DateTime min, DateTime max)
        {
            while (true)
            {
                Console.Write($"{label} (yyyy-MM-dd) [{defaultValue:yyyy-MM-dd}]: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    return defaultValue;
                }

                if (DateTime.TryParseExact(
                        input.Trim(),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var date)
                    && date >= min
                    && date <= max)
                {
                    return date;
                }

                Console.WriteLine($"Invalid date, it must be between {min:yyyy-MM-dd} and {max:yyyy-MM-dd}, please try again!");
            }
        }

        private double ReadDouble(string label, double? defaultValue, Func<double, bool> isValid)
        {
            while (true)
            {
                var defaultText = defaultValue.HasValue
                    ? $" [{defaultValue.Value.ToString(CultureInfo.InvariantCulture)}]"
                    : string.Empty;

                Console.Write($"{label}{defaultText}: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input) && defaultValue.HasValue)
                {
                    return defaultValue.Value;
                }

                if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    && isValid(value))
                {
                    return value;
                }

                Console.WriteLine("Invalid value, please try again!");
            }
        }

        private string BuildOptions(string zeroOption, params IEnumerable<string> options)
        {
            var builtOptions = options
                .Select((x, i) => $"{i + 1}. {x}")
                .ToList();

            builtOptions.Add($"0. {zeroOption}");

            return string.Join("\n", builtOptions);
        }
    }
}
