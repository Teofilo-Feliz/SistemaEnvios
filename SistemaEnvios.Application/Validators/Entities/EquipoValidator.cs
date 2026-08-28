using FluentValidation;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Validators.Entities;

public sealed class EquipoValidator : AbstractValidator<Equipo>
{
    public EquipoValidator()
    {
        RuleFor(x => x.TipoEquipoId).GreaterThan(0);
        RuleFor(x => x.UbicacionActualId).GreaterThan(0);
        RuleFor(x => x.Marca).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Modelo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CodigoActivo).MaximumLength(50);
        RuleFor(x => x.NumeroSerie).MaximumLength(100);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
