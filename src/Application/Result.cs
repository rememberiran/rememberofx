namespace Application;

public class DomainError
{
    public string Code { get; }
    public string Message { get; }
    public int StatusCode { get; }

    private DomainError(string code, string message, int statusCode)
    {
        Code = code;
        Message = message;
        StatusCode = statusCode;
    }

    public static DomainError Validation(string message) => new("VALIDATION_ERROR", message, 400);
    public static DomainError Unauthorized(string message) => new("UNAUTHORIZED", message, 401);
    public static DomainError Forbidden(string message) => new("FORBIDDEN", message, 403);
    public static DomainError NotFound(string message) => new("NOT_FOUND", message, 404);
    public static DomainError Conflict(string message) => new("CONFLICT", message, 409);
}

public class Result
{
    public bool IsSuccess { get; }
    public DomainError? Error { get; }

    protected Result(bool isSuccess, DomainError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(DomainError error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value);
    public static Result<T> Failure<T>(DomainError error) => new(error);
}

public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T value) : base(true, null) => Value = value;
    internal Result(DomainError error) : base(false, error) { }
}
