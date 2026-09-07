namespace SistemaEnvios.Application.Common;

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    BusinessRule = 6,
    /// <summary>
    /// Un sistema de terceros del que dependemos falló (caído, lento, credenciales vencidas).
    /// No es culpa de quien llamó ni una regla de negocio: se distingue para que la respuesta
    /// sea 502 y el usuario sepa que reintentar puede funcionar.
    /// </summary>
    ExternalService = 7
}

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public ErrorType ErrorType { get; }
    public bool IsFailure => !IsSuccess;

    protected Result(bool success, string? error, ErrorType errorType)
    {
        IsSuccess = success;
        Error = error;
        ErrorType = errorType;
    }

    public static Result Success() => new(true, null, ErrorType.None);
    public static Result Failure(string error, ErrorType errorType = ErrorType.BusinessRule) =>
        new(false, error, errorType);
}

public class Result<T> : Result
{
    public T? Value { get; }
    private Result(T value) : base(true, null, ErrorType.None) => Value = value;
    private Result(string error, ErrorType errorType) : base(false, error, errorType) { }
    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(string error, ErrorType errorType = ErrorType.BusinessRule) =>
        new(error, errorType);
}
