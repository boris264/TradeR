namespace TradeR.SharedKernel.Results
{
    /// <summary>Describes why an operation failed. <see cref="None"/> means no error.</summary>
    public sealed record Error(string Code, string Message)
    {
        public static readonly Error None = new Error(string.Empty, string.Empty);

        public override string ToString()
        {
            return $"{Code}: {Message}";
        }
    }
}
