using System;

namespace TradeR.SharedKernel.Extensions
{
    public static class ArgumentExceptionExtensions
    {
        extension(ArgumentException)
        {
            public static void ThrowIfNotUtc(DateTime dateTime, string? paramName)
            {
                if (dateTime.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentException("DateTime parameter must be in UTC format", paramName);
                }
            }
        }
    }
}
