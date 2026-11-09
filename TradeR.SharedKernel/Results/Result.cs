using System;

namespace TradeR.SharedKernel.Results
{
    /// <summary>The outcome of an operation that either succeeds or fails with an <see cref="Results.Error"/>.</summary>
    public class Result
    {
        protected Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
            {
                throw new ArgumentException("A successful result cannot have an error.", nameof(error));
            }

            if (!isSuccess && error == Error.None)
            {
                throw new ArgumentException("A failed result must have an error.", nameof(error));
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public Error Error { get; }

        public static Result Success()
        {
            return new Result(true, Error.None);
        }

        public static Result Failure(Error error)
        {
            return new Result(false, error);
        }

        public static Result<TValue> Success<TValue>(TValue value)
        {
            return new Result<TValue>(value, true, Error.None);
        }

        public static Result<TValue> Failure<TValue>(Error error)
        {
            return new Result<TValue>(default, false, error);
        }
    }
}
