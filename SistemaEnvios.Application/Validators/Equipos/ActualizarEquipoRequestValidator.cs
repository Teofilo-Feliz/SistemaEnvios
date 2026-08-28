using FluentValidation;
using SistemaEnvios.Application.DTOs.Equipos;

namespace SistemaEnvios.Application.Validators.Equipos;

public sealed class ActualizarEquipoRequestValidator : AbstractValidator<ActualizarEquipoRequest>
{
    public ActualizarEquipoRequestValidator()
    {
        RuleFor(x => x.EquipoId).GreaterThan(0);
        RuleFor(x => x.TipoEquipoId).GreaterThan(0);
        RuleFor(x => x.UbicacionActualId).GreaterThan(0);
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CodigoActivo).MaximumLength(50);
        RuleFor(x => x.NumeroSerie).MaximumLength(100);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
