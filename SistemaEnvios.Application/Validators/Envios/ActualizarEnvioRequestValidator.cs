using FluentValidation;
using SistemaEnvios.Application.DTOs.Envios;

namespace SistemaEnvios.Application.Validators.Envios;

public sealed class ActualizarEnvioRequestValidator : AbstractValidator<ActualizarEnvioRequest>
{
    public ActualizarEnvioRequestValidator()
    {
        RuleFor(x => x.EnvioId).GreaterThan(0);
        RuleFor(x => x.UbicacionOrigenId).GreaterThan(0);
        RuleFor(x => x.UbicacionDestinoId).GreaterThan(0).NotEqual(x => x.UbicacionOrigenId);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
