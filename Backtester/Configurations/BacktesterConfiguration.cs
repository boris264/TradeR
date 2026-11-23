namespace Backtester.Configurations
{
    public sealed class BacktesterConfiguration
    {
        public const string SectionName = "Backtester";

        public double Fee { get; set; } = 0.008;
    }
}
