using System;
using TradeR.SharedKernel.Results;

namespace TradeR.SharedKernel.Indicators
{
    /// <summary>Errors returned by technical indicators. Codes are prefixed with <c>Indicators.</c>.</summary>
    public static class IndicatorErrors
    {
        public static Error InvalidPeriod(int period)
        {
            return new Error("Indicators.InvalidPeriod", $"The period must be greater than 0, got {period}.");
        }

        public static Error NotEnoughData(int count, int period)
        {
            return new Error("Indicators.NotEnoughData", $"At least {period} value(s) are needed for period {period}, got {count}.");
        }

        public static Error SessionOpenCloseTimeMismatch(TimeOnly sessionOpen, TimeOnly sessionClose)
        {
            return new Error(
                "Indicators.SessionOpenCloseTimeMismatch",
                $"The opening time of the session {sessionOpen} cannot be after the closing time {sessionClose}.");
        }
    }
}
