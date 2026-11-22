using System;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Extensions;

namespace Backtester.Engine
{
    internal class EngineConfiguration
    {
        public EngineConfiguration(
            string assetPair,
            string strategyName,
            DateTime from,
            DateTime to,
            MarketInterval interval,
            double riskSize,
            double initialBalance = 5000.0)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(assetPair, nameof(assetPair));
            ArgumentException.ThrowIfNullOrWhiteSpace(strategyName, nameof(strategyName));
            ArgumentException.ThrowIfNotUtc(from, nameof(from));
            ArgumentException.ThrowIfNotUtc(to, nameof(to));

            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initialBalance, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(riskSize, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(riskSize, 0);

            AssetPair = assetPair;
            StrategyName = strategyName;
            From = from;
            To = to;
            Interval = interval;
            InitialBalance = initialBalance;
            RiskSize = riskSize;
        }
        public string AssetPair { get; init; }

        public string StrategyName { get; init; }

        public DateTime From { get; init; }

        public DateTime To { get; init; }

        public MarketInterval Interval { get; init; }

        public double InitialBalance { get; init; }

        public double RiskSize { get; init; }
    }
}
