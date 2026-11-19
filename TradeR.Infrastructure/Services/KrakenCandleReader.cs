using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TradeR.Infrastructure.Mappings;
using TradeR.Infrastructure.Repositories.Interfaces;
using TradeR.Services.Interfaces;
using TradeR.Services.Models.Candles;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Extensions;

namespace TradeR.Infrastructure.Services
{
    internal class KrakenCandleReader : ICandleReader
    {
        private readonly IKrakenOhlcvtRepository krakenOhlcvtRepository;

        public KrakenCandleReader(IKrakenOhlcvtRepository krakenOhlcvtRepository)
        {
            this.krakenOhlcvtRepository = krakenOhlcvtRepository;
        }

        public IAsyncEnumerable<OhlcvtCandle> StreamCandles(
            string assetPair,
            DateTime from,
            DateTime to,
            MarketInterval interval,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(nameof(assetPair));
            ArgumentException.ThrowIfNotUtc(from, nameof(from));
            ArgumentException.ThrowIfNotUtc(to, nameof(to));

            if (from > to)
            {
                throw new ArgumentException("The start of the period must not be after its end", nameof(from));
            }

            var fromToUnixTimestamp = new DateTimeOffset(from).ToUnixTimeSeconds();
            var toToUnixTimestamp = new DateTimeOffset(to).ToUnixTimeSeconds();

            var dbCandlesStream = krakenOhlcvtRepository.StreamAssetPairCandlesForTimespan(
                assetPair,
                fromToUnixTimestamp,
                toToUnixTimestamp,
                (int)interval,
                cancellationToken);

            return AsyncEnumerable.Select(
                dbCandlesStream,
                x => x.ToOhlcvtCandle());
        }
    }
}
