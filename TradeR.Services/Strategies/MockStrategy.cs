using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Signals;
using TradeR.SharedKernel.Enums;

namespace TradeR.Services.Strategies
{
    public class MockStrategy : Strategy
    {
        private int processedCandles = 0;

        public override string Name => "Mock Strategy used for testing";

        public override int CandlesToPreload => 0;

        public override Signal? Next(OhlcvtCandle candle)
        {
            if (processedCandles == 10 && state == StrategyState.Idle)
            {
                return new EntrySignal()
                {
                    Direction = PositionDirection.Long,
                    StopLossPrice = candle.Low - candle.Low * 0.05,
                    Reason = "Mock strategy"
                };
            }

            if (processedCandles == 20 && state == StrategyState.LongPositionOpened)
            {
                return new ExitSignal()
                {
                    Reason = "Test exit"
                };
            }

            processedCandles++;

            return null;
        }
    }
}
