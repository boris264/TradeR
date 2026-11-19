using System;
using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Results;

namespace TradeR.Services.Validators
{
    public static class MarketDataLoadRequestValidator
    {
        public static Result Validate(this MarketDataLoadRequest source)
        {
            if (source == null)
            {
                return Result.Failure(new Error("MarketDataLoadRequest", "Request cannot be null!"));
            }

            if (string.IsNullOrWhiteSpace(source.AssetPair))
            {
                return Result.Failure(new Error("MarketDataLoadRequest.AssetPair", "AssetPair cannot be null or empty!"));
            }

            if (source.From.Kind != DateTimeKind.Utc)
            {
                return Result.Failure(new Error("MarketDataLoadRequest.From", "DateTime From should be in UTC!"));
            }

            if (source.To.Kind != DateTimeKind.Utc)
            {
                return Result.Failure(new Error("MarketDataLoadRequest.To", "DateTime From should be in UTC!"));
            }

            if (source.CandlesToPreload < 0)
            {
                return Result.Failure(new Error("MarketDataLoadRequest.CandlesToPreload", "CandlesToPreload should not be less than 0!"));
            }

            return Result.Success();
        }
    }
}
