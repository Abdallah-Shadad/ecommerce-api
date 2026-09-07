namespace ECommerce.Shared.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? value, string? errorMessage, IEnumerable<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorMessage = errorMessage;
        Errors = errors?.ToList().AsReadOnly() ?? (errorMessage != null ? new List<string> { errorMessage }.AsReadOnly() : new List<string>().AsReadOnly());
    }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string errorMessage, IEnumerable<string>? errors = null) =>
        new(false, default, errorMessage, errors);

    public static implicit operator Result<T>(T value) => Success(value);
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, string? errorMessage, IEnumerable<string>? errors = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Errors = errors?.ToList().AsReadOnly() ?? (errorMessage != null ? new List<string> { errorMessage }.AsReadOnly() : new List<string>().AsReadOnly());
    }

    public static Result Success() => new(true, null);

    public static Result Failure(string errorMessage, IEnumerable<string>? errors = null) =>
        new(false, errorMessage, errors);
}
