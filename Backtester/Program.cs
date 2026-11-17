using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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

            var host = hostBuilder.Build();
        }
    }
}
