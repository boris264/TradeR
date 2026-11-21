using System;

namespace Backtester.Models
{
    internal class Account
    {
        public string? Name { get; init; }

        public double Balance { get; set; }

        /// <summary>
        /// The percentage amount of the available balance to risk on each trade. Should be a value
        /// between 0 and 1
        /// </summary>
        public double RiskSize
        {
            get;
            init
            {
                if (value < 0 || value > 1)
                {
                    throw new ArgumentException("RiskSize should be between 0 and 1", nameof(RiskSize));
                }

                field = value;
            }
        }

        public double RiskAmount => Balance * RiskSize;
    }
}
