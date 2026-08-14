using Screener.Services.Interfaces;

namespace Screener.Services
{
    internal class HistoricalDataScreener : IScreener
    {
        public string Description { get; } = "Get historical data for an asset";
    }
}
