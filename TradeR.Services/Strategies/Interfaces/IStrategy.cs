using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.Services.Models.Signals;

namespace TradeR.Services.Strategies.Interfaces
{
    public interface IStrategy
    {
        /// <summary>
        /// Name of the strategy, e.g. VWAP x 9 EMA Cross
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Provides the next candle for the strategy
        /// </summary>
        /// <param name="candle">The OHLCVT candle</param>
        /// <returns>A signal, if the strategy's conditions are met, null otherwise</returns>
        Signal? Next(OhlcvtCandle candle);

        /// <summary>
        /// Tells the strategy an order was placed
        /// </summary>
        /// <param name="order">Information about the order</param>
        void PlaceOrder(Order order);
    }
}
