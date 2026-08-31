using FluentValidation;
using SistemaEnvios.Application.DTOs.Transportes;

namespace SistemaEnvios.Application.Validators.Transportes;

public sealed class ActualizarTransporteRequestValidator : AbstractValidator<ActualizarTransporteRequest>
{
    public ActualizarTransporteRequestValidator()
    {
        RuleFor(x => x.TransporteId).GreaterThan(0);
        RuleFor(x => x.TipoTransporteId).GreaterThan(0);
        RuleFor(x => x.NombreResponsable).MaximumLength(150);
        RuleFor(x => x.Parentesco).MaximumLength(50);
        RuleFor(x => x.CedulaResponsable).Matches("^[0-9]{11}$").When(x => !string.IsNullOrWhiteSpace(x.CedulaResponsable));
        RuleFor(x => x.PlacaVehiculo).MaximumLength(20);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
