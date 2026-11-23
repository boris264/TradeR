using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Signals;
using TradeR.Services.Strategies.Interfaces;
using TradeR.SharedKernel.Enums;

namespace TradeR.Services.Strategies
{
    public class MockStrategy : IStrategy
    {
        private StrategyState state = StrategyState.Idle;
        private int processedCandles = 0;

        public string Name => "Mock Strategy used for testing";

        public int CandlesToPreload => 0;

        public Signal? Next(OhlcvtCandle candle)
        {
            if (processedCandles == 10 && state == StrategyState.Idle)
            {
                return new EntrySignal()
                {
                    Direction = PositionDirection.Long,
                    StopLossPrice = candle.Low - candle.Low * 0.05
                };
            }

            if (processedCandles == 20 && state == StrategyState.PositionOpened)
            {
                return new ExitSignal()
                {
                    Reason = "Test exit"
                };
            }

            processedCandles++;

            return null;
        }

        public void ChangeState(StrategyState newState)
        {
            state = newState;
        }
    }
}
