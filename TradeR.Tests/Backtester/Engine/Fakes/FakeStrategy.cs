using System.Collections.Generic;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Signals;
using TradeR.Services.Strategies;
using TradeR.Services.Strategies.Interfaces;

namespace TradeR.Tests.Backtester.Engine.Fakes
{
    internal sealed class FakeStrategy : IStrategy
    {
        private readonly Dictionary<long, Signal> signals = new Dictionary<long, Signal>();

        public FakeStrategy(string name, int candlesToPreload = 0)
        {
            Name = name;
            CandlesToPreload = candlesToPreload;
        }

        public string Name { get; }

        public int CandlesToPreload { get; }

        public List<OhlcvtCandle> ReceivedCandles { get; } = new List<OhlcvtCandle>();

        public FakeStrategy SignalOn(long timestamp, Signal signal)
        {
            signals[timestamp] = signal;

            return this;
        }

        public Signal? Next(OhlcvtCandle candle)
        {
            ReceivedCandles.Add(candle);

            return signals.TryGetValue(candle.Timestamp, out Signal? signal)
                ? signal
                : null;
        }

        public void ChangeState(StrategyState newState)
        {
        }
    }
}
