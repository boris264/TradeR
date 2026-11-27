namespace TradeR.Services.Models.Signals
{
    public abstract class Signal
    {
        public SignalType Type { get; set; }

        /// <summary>
        /// The reason this signal was given, e.g. 2 EMA Cross
        /// </summary>
        public required string Reason { get; set; }

        protected Signal(SignalType signalType)
        {
            Type = signalType;
        }
    }
}
