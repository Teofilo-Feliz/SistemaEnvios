using FluentValidation;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Validators.Entities;

public sealed class TransicionEstadoEnvioValidator : AbstractValidator<TransicionEstadoEnvio>
{
    public TransicionEstadoEnvioValidator()
    {
        RuleFor(x => x.EstadoOrigenId).GreaterThan(0);
        RuleFor(x => x.EstadoDestinoId)
            .GreaterThan(0)
            .NotEqual(x => x.EstadoOrigenId);
    }
}
