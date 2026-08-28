using FluentValidation;
using SistemaEnvios.Application.DTOs.Ubicaciones;

namespace SistemaEnvios.Application.Validators.Ubicaciones;

public sealed class GuardarUbicacionRequestValidator : AbstractValidator<GuardarUbicacionRequest>
{
    public GuardarUbicacionRequestValidator()
    {
        RuleFor(x => x.UbicacionId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CodigoCentro).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Tipo).IsInEnum();
    }
}
