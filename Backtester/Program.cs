using Backtester.Menu;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using TradeR.Infrastructure.DependencyInjection;

namespace Backtester
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var hostBuilder = Host.CreateApplicationBuilder();

            hostBuilder.Configuration
                .AddUserSecrets<Program>();

            hostBuilder.Services.AddInfrastructureServices(hostBuilder.Configuration);
            hostBuilder.Services.AddSingleton<BacktestingMenu>();

            var host = hostBuilder.Build();

            using var scope = host.Services.CreateScope();

            var menu = scope.ServiceProvider.GetRequiredService<BacktestingMenu>();

            await menu.PrepareBacktest(CancellationToken.None);
        }
    }
}
