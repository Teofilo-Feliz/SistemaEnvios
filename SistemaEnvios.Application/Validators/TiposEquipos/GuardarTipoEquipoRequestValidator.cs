using FluentValidation;
using SistemaEnvios.Application.DTOs.TiposEquipos;

namespace SistemaEnvios.Application.Validators.TiposEquipos;

public sealed class GuardarTipoEquipoRequestValidator : AbstractValidator<GuardarTipoEquipoRequest>
{
    public GuardarTipoEquipoRequestValidator()
    {
        RuleFor(x => x.TipoEquipoId).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
    }
}
