namespace ECommerce.Shared.Common;

public record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data = default,
    IReadOnlyList<string>? Errors = null
)
{
    public static ApiResponse<T> Ok(T data, string message = "Request completed successfully.") =>
        new(true, message, data, null);

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new(false, message, default, errors);
}

public record ApiResponse(
    bool Success,
    string Message,
    IReadOnlyList<string>? Errors = null
)
{
    public static ApiResponse Ok(string message = "Request completed successfully.") =>
        new(true, message, null);

    public static ApiResponse Fail(string message, IReadOnlyList<string>? errors = null) =>
        new(false, message, errors);
}
