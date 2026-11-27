using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Signals;
using TradeR.Services.Strategies.Interfaces;

namespace TradeR.Services.Strategies
{
    public abstract class Strategy : IStrategy
    {
        protected StrategyState state;

        public abstract string Name { get; }

        public abstract int CandlesToPreload { get; }

        public virtual void ChangeState(StrategyState newState)
        {
            state = newState;
        }

        public abstract Signal? Next(OhlcvtCandle candle);
    }
}
