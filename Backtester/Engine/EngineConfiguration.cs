using System;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Extensions;

namespace Backtester.Engine
{
    internal class EngineConfiguration
    {
        public EngineConfiguration(
            string assetPair,
            DateTime from,
            DateTime to,
            MarketInterval interval,
            double riskSize,
            double initialBalance = 5000.0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assetPair, nameof(assetPair));
            ArgumentException.ThrowIfNotUtc(from, nameof(from));
            ArgumentException.ThrowIfNotUtc(to, nameof(to));

            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialBalance, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(riskSize, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(riskSize, 0);

            AssetPair = assetPair;
            From = from;
            To = to;
            Interval = interval;
            InitialBalance = initialBalance;
            RiskSize = riskSize;
        }
        public required string AssetPair { get; init; }

        public DateTime From { get; init; }

        public DateTime To { get; init; }

        public MarketInterval Interval { get; init; }

        public double InitialBalance { get; init; }

        public double RiskSize { get; init; }
    }
}
