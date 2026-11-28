using System;
using System.Collections.Generic;
using TradeR.Services.Models.Market;

namespace Backtester.Configurations
{
    public class BacktestsConfiguration
    {
        public const string SectionName = "Backtests";

        public List<BacktestConfiguration> Backtests { get; set; } = new List<BacktestConfiguration>();
    }

    public class BacktestConfiguration
    {
        public required string AssetPair { get; set; }

        public required string StrategyName { get; set; }

        public MarketInterval Interval { get; set; }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public double InitialBalance { get; set; }

        public double RiskSize { get; set; }
    }
}
