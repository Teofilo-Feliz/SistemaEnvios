using FluentValidation.Results;

namespace SistemaEnvios.Application.Common;

public static class ValidationExtensions
{
    public static string ToErrorMessage(this ValidationResult result)
    {
        return string.Join(" ", result.Errors.Select(error => error.ErrorMessage));
    }
}
