using Microsoft.AspNetCore.Mvc;
using SistemaEnvios.Application.Common;

namespace SistemaEnvios.Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.NoContent();

        return ToProblem(result, controller);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);

        return ToProblem(result, controller);
    }

    public static IActionResult ToCreatedAtActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        string actionName,
        object? routeValues = null)
    {
        if (result.IsSuccess)
            return controller.CreatedAtAction(actionName, routeValues, result.Value);

        return ToProblem(result, controller);
    }

    private static ObjectResult ToProblem(Result result, ControllerBase controller)
    {
        var statusCode = result.ErrorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.ExternalService => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status422UnprocessableEntity
        };

        return controller.Problem(
            detail: result.Error,
            statusCode: statusCode,
            title: GetTitle(result.ErrorType));
    }

    private static string GetTitle(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => "Solicitud inválida",
        ErrorType.Unauthorized => "No autenticado",
        ErrorType.Forbidden => "Acceso denegado",
        ErrorType.NotFound => "Recurso no encontrado",
        ErrorType.Conflict => "Conflicto con el estado actual",
        ErrorType.ExternalService => "Servicio externo no disponible",
        _ => "Regla de negocio no satisfecha"
    };
}
