using Microsoft.Extensions.Logging;
using System;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Signals;
using TradeR.SharedKernel.Enums;
using TradeR.SharedKernel.Indicators;

namespace TradeR.Services.Strategies
{
    /// <summary>
    /// Trades pullbacks to the 200-period EMA of the closing prices. It gives no signals until the EMA
    /// has a value (the first 199 candles).
    /// <para>
    /// Long, with no position open: the candle opens at or above the EMA, its low reaches the EMA, and it
    /// closes above the EMA. The stop loss is the candle's low.
    /// </para>
    /// <para>
    /// Short, with no position open: the candle opens at or below the EMA, its high reaches the EMA, and it
    /// closes below the EMA. The stop loss is the candle's high.
    /// </para>
    /// <para>
    /// No take profit is set. With a position open, it exits a long when a candle closes at or below the EMA,
    /// and a short when a candle closes at or above it.
    /// </para>
    /// </summary>
    public sealed class TwoHundredEmaFollowingStrategy : Strategy
    {
        private const double stopLossDeviation = 0.001; // 0.1%

        private readonly ExponentialMovingAverage ema;
        private readonly ILogger<TwoHundredEmaFollowingStrategy> logger;

        public override string Name => "200 EMA Following";

        // Preload more candles, so we can build almost the same EMA value as other platforms
        // like Tradingview, since their charts load more than the 200 required
        // candles
        public override int CandlesToPreload => 1200;

        public TwoHundredEmaFollowingStrategy(
            ILogger<TwoHundredEmaFollowingStrategy> logger)
        {
            var createEmaResult = ExponentialMovingAverage.Create(period: 200);

            if (createEmaResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Failed to create the ExponentialMovingAverage, message: {createEmaResult.Error}");
            }

            ema = createEmaResult.Value;
            this.logger = logger;
        }

        public override Signal? Next(OhlcvtCandle candle)
        {
            var currentEma = ema.Add(candle.Close);

            if (currentEma == null)
            {
                logger.LogDebug("EMA returned null, data is being preloaded.");
                return null;
            }

            if (state == StrategyState.Idle)
            {
                // Candle is above EMA, look for longs
                if (candle.Open >= currentEma && candle.Close > currentEma)
                {
                    return LookForLongs(candle, currentEma.Value);
                }
                // Candle is below EMA, look for shorts
                else if (candle.Open <= currentEma && candle.Close < currentEma)
                {
                    return LookForShorts(candle, currentEma.Value);
                }
            }
            else if (state == StrategyState.LongPositionOpened)
            {
                return LookToCloseLongPosition(candle, currentEma.Value);
            }
            else if (state == StrategyState.ShortPositionOpened)
            {
                return LookToCloseShortPosition(candle, currentEma.Value);
            }

            return null;
        }

        private static EntrySignal? LookForLongs(OhlcvtCandle candle, double ema)
        {
            if (candle.Low <= ema)
            {
                return new EntrySignal()
                {
                    Direction = PositionDirection.Long,
                    StopLossPrice = candle.Low * (1 - stopLossDeviation),
                    Reason = $"Candle low {candle.High} was less than 200 EMA {ema} and price closed above EMA"
                };
            }

            return null;
        }

        private static EntrySignal? LookForShorts(OhlcvtCandle candle, double ema)
        {
            if (candle.High >= ema)
            {
                return new EntrySignal()
                {
                    Direction = PositionDirection.Short,
                    StopLossPrice = candle.High * (1 + stopLossDeviation),
                    Reason = $"Candle high {candle.High} was more than 200 EMA {ema} and price closed below EMA"
                };
            }

            return null;
        }

        private static ExitSignal? LookToCloseLongPosition(OhlcvtCandle candle, double ema)
        {
            if (candle.Close <= ema)
            {
                return new ExitSignal()
                {
                    Reason = $"Closing price {candle.Close} was less than 200 EMA {ema}"
                };
            }

            return null;
        }

        private static ExitSignal? LookToCloseShortPosition(OhlcvtCandle candle, double ema)
        {
            if (candle.Close >= ema)
            {
                return new ExitSignal()
                {
                    Reason = $"Closing price {candle.Close} was more than 200 EMA {ema}"
                };
            }

            return null;
        }
    }
}
