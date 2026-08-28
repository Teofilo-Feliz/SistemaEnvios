using FluentValidation;
using SistemaEnvios.Application.DTOs.Transportes;

namespace SistemaEnvios.Application.Validators.Transportes;

public sealed class ActualizarTransporteRequestValidator : AbstractValidator<ActualizarTransporteRequest>
{
    public ActualizarTransporteRequestValidator()
    {
        RuleFor(x => x.TransporteId).GreaterThan(0);
        RuleFor(x => x.Tipo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NombreChofer).MaximumLength(150);
        RuleFor(x => x.Placa).MaximumLength(20);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
