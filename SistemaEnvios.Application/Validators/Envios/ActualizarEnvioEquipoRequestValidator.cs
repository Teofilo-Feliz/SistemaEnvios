using FluentValidation;
using SistemaEnvios.Application.DTOs.Envios;

namespace SistemaEnvios.Application.Validators.Envios;

public sealed class ActualizarEnvioEquipoRequestValidator : AbstractValidator<ActualizarEnvioEquipoRequest>
{
    public ActualizarEnvioEquipoRequestValidator()
    {
        RuleFor(x => x.EnvioEquipoId).GreaterThan(0);
        RuleFor(x => x.NumeroTicket).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Observaciones).NotEmpty().MaximumLength(2000);
    }
}
