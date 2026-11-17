using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TradeR.Infrastructure.Repositories.Interfaces;
using TradeR.Services.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;

namespace TradeR.Infrastructure.Services
{
    internal class KrakenCandleReader : ICandleReader
    {
        private readonly IKrakenOhlcvtRepository krakenOhlcvtRepository;

        public KrakenCandleReader(IKrakenOhlcvtRepository krakenOhlcvtRepository)
        {
            this.krakenOhlcvtRepository = krakenOhlcvtRepository;
        }

        public IAsyncEnumerable<OhlcvtCandle> Load(
            string assetPair,
            DateTime from,
            DateTime to,
            MarketInterval interval,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(assetPair))
            {
                throw new ArgumentException("Parameter should not be null or empty", nameof(assetPair));
            }

            if (from.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("DateTime should be in UTC", nameof(from));
            }

            if (to.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("DateTime should be in UTC", nameof(to));
            }

            if (from > to)
            {
                throw new ArgumentException("The start of the period must not be after its end", nameof(from));
            }

            var fromToUtcTimestamp = new DateTimeOffset(from).ToUnixTimeSeconds();
            var toToUtcTimestamp = new DateTimeOffset(to).ToUnixTimeSeconds();

            var dbCandlesStream = krakenOhlcvtRepository.GetAssetPairCandlesForTimespan(
                assetPair,
                fromToUtcTimestamp,
                toToUtcTimestamp,
                (int)interval,
                cancellationToken);

            return AsyncEnumerable.Select(
                dbCandlesStream,
                x => new OhlcvtCandle(
                    Timestamp: x.Timestamp,
                    Open: x.Open,
                    Close: x.Close,
                    High: x.High,
                    Low: x.Low,
                    Volume: x.Volume,
                    Trades: x.Trades));
        }
    }
}
