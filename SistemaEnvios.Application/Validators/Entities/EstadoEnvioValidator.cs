using FluentValidation;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Validators.Entities;

public sealed class EstadoEnvioValidator : AbstractValidator<EstadoEnvio>
{
    public EstadoEnvioValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Descripcion)
            .MaximumLength(500);
    }
}
