namespace SistemaEnvios.Application.Common;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    protected Result(bool success, string? error) { IsSuccess = success; Error = error; }
    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

public class Result<T> : Result
{
    public T? Value { get; }
    private Result(T value) : base(true, null) => Value = value;
    private Result(string error) : base(false, error) { }
    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(string error) => new(error);
}
