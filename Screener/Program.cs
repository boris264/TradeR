using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Screener.Services;
using Screener.Services.Interfaces;
using System.Threading.Tasks;

namespace Screener
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddScoped<IScreener, HistoricalDataScreener>();
            builder.Services.AddSingleton<StartMenu>();

            var app = builder.Build();

            var startMenu = app.Services.GetRequiredService<StartMenu>();
            await startMenu.RunAsync();
        }
    }
}
