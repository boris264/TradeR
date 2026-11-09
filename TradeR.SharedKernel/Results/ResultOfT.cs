using System;

namespace TradeR.SharedKernel.Results
{
    /// <summary>The outcome of an operation that returns a <typeparamref name="TValue"/> on success.</summary>
    public sealed class Result<TValue> : Result
    {
        private readonly TValue? _value;

        internal Result(TValue? value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        /// <summary>The value of a successful result. Throws when read on a failure; check <see cref="Result.IsSuccess"/> first.</summary>
        public TValue Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error}).");

        public static implicit operator Result<TValue>(TValue value)
        {
            return Success(value);
        }

        public static implicit operator Result<TValue>(Error error)
        {
            return Failure<TValue>(error);
        }
    }
}
