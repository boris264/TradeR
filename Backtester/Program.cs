using Backtester.Backtester;
using Backtester.Backtester.Interfaces;
using Backtester.Configurations;
using Backtester.Engine;
using Backtester.Menu;
using Backtester.Menu.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TradeR.Infrastructure.DependencyInjection;
using TradeR.Services.Market;
using TradeR.Services.Market.Interfaces;
using TradeR.Services.Strategies;
using TradeR.Services.Strategies.Interfaces;

namespace Backtester
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var hostBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings()
            {
                ContentRootPath = AppContext.BaseDirectory
            });

            hostBuilder.Configuration
                .AddUserSecrets<Program>()
                .AddJsonFile("backtests.json", optional: true);

            hostBuilder.Services
                .AddOptions<BacktesterConfiguration>()
                .Bind(hostBuilder.Configuration.GetSection(BacktesterConfiguration.SectionName))
                .Validate(
                    settings => settings.Fee > 0 && settings.Fee <= 1,
                    $"{BacktesterConfiguration.SectionName}:Fee must be greater than 0 and at most 1.")
                .ValidateOnStart();

            hostBuilder.Services
                .AddOptions<BacktestsConfiguration>()
                .Bind(hostBuilder.Configuration.GetSection(BacktestsConfiguration.SectionName));

            hostBuilder.Services.AddInfrastructureServices(hostBuilder.Configuration);
            hostBuilder.Services.AddSingleton<IMenu<IEnumerable<EngineConfiguration>>, BacktestingMenu>();
            hostBuilder.Services.AddSingleton<ConsoleWorker>();
            hostBuilder.Services.AddScoped<IBacktester<EngineConfiguration>, HistoricalDataBacktester>();
            hostBuilder.Services.AddScoped<IMarketDataLoader, MarketDataLoader>();

            hostBuilder.Services.AddScoped<IStrategy, MockStrategy>();
            hostBuilder.Services.AddScoped<IStrategy, TwoHundredEmaFollowingStrategy>();

            var host = hostBuilder.Build();

            try
            {
                host.Services.GetRequiredService<IStartupValidator>().Validate();
            }
            catch (OptionsValidationException e)
            {
                Console.Error.WriteLine(e.Message);
                Environment.ExitCode = 1;

                return;
            }

            using var scope = host.Services.CreateScope();

            var consoleWorker = scope.ServiceProvider.GetRequiredService<ConsoleWorker>();
            await consoleWorker.StartAsync();
        }
    }
}
