namespace ModularCommerce.Shared.Kernel;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None ||
            !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error state", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Failure result has no value");
}

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
}

/// <param name="Retryable">
/// Geçici (transient) çakışma mı? true ise istemci AYNI Idempotency-Key ile tekrar denemelidir
/// (retryable-409 sözleşmesi); false terminal hatadır. HTTP katmanı bunu yanıt gövdesine
/// yapısal olarak yansıtır — istemci kod string'i eşleştirmek zorunda kalmaz.
/// </param>
public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Failure,
    bool Retryable = false)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message, bool retryable = false)
        => new(code, message, ErrorType.Conflict, retryable);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
