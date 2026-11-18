using System;
using TradeR.Services.Models.Market;

namespace Backtester.Engine
{
    internal class EngineConfiguration
    {
        public EngineConfiguration(
            string assetPair,
            DateTime from,
            DateTime to,
            MarketInterval interval,
            double initialBalance = 5000.0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assetPair, nameof(assetPair));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialBalance, 0);

            if (from.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Parameter must be in UTC format!", nameof(from));
            }

            if (to.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Parameter must be in UTC format!", nameof(to));
            }

            AssetPair = assetPair;
            From = from;
            To = to;
            Interval = interval;
            InitialBalance = initialBalance;
        }
        public required string AssetPair { get; init; }

        public DateTime From { get; init; }

        public DateTime To { get; init; }

        public MarketInterval Interval { get; init; }

        public double InitialBalance { get; init; }
    }
}
