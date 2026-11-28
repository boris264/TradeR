using System;

namespace TradeR.SharedKernel.Indicators
{
    public sealed class RelativeStrengthIndex
    {
        private readonly int period;

        private int processedPrices = 0;
        private double? previousClose = null;
        private double? previousAverageGain = null;
        private double? previousAverageLoss = null;

        private double averageGainSum = 0;
        private double averageLossSum = 0;

        public double? Rsi { get; private set; }

        public RelativeStrengthIndex(int period = 14)
        {
            this.period = period;
        }

        public double? Add(double closePrice)
        {
            if (previousClose == null)
            {
                processedPrices++;
                previousClose = closePrice;

                return null;
            }

            var positiveChange = 0.0;
            var negativeChange = 0.0;

            if (closePrice > previousClose)
            {
                positiveChange = closePrice - previousClose.Value;
            }
            else if (closePrice < previousClose)
            {
                negativeChange = previousClose.Value - closePrice;
            }

            double? averageGain = null;
            double? averageLoss = null;

            if (processedPrices < period)
            {
                averageGainSum += positiveChange;
                averageLossSum += negativeChange;
            }
            else if (processedPrices == period)
            {
                averageGainSum += positiveChange;
                averageLossSum += negativeChange;

                averageGain = averageGainSum / processedPrices;
                averageLoss = averageLossSum / processedPrices;

                var rs = averageGain / averageLoss;

                Rsi = Math.Round((double)(100 - (100 / (1 + rs))), 2);
            }
            else
            {
                averageGain = (previousAverageGain * (period - 1) + positiveChange) / period ?? 0;
                averageLoss = (previousAverageLoss * (period - 1) + negativeChange) / period ?? 0;

                var rs = averageGain / averageLoss;

                Rsi = Math.Round((double)(100 - (100 / (1 + rs))), 2);
            }

            previousClose = closePrice;
            processedPrices++;
            previousAverageGain = averageGain;
            previousAverageLoss = averageLoss;

            return Rsi;
        }
    }
}
