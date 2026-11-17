using System;
using System.Collections.Generic;
using TradeR.Services.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;

namespace TradeR.Infrastructure.Services
{
    internal class CandleReader : ICandleReader
    {
        public IAsyncEnumerable<OhlcvtCandle> Load(
            string assetPair,
            DateTime from,
            DateTime to,
            MarketInterval interval)
        {
            throw new NotImplementedException();
        }
    }
}
