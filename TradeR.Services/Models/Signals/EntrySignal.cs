using TradeR.SharedKernel.Enums;

namespace TradeR.Services.Models.Signals
{
    public sealed class EntrySignal : Signal
    {
        public EntrySignal() : base(SignalType.Entry)
        {
        }

        public PositionDirection Direction { get; init; }

        public double? TakeProfitPrice { get; init; }

        public double StopLossPrice { get; init; }
    }
}
