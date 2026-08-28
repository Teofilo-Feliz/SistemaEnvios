using FluentValidation;
using SistemaEnvios.Domain.Entities;

namespace SistemaEnvios.Application.Validators.Entities;

public sealed class TipoEquipoValidator : AbstractValidator<TipoEquipo>
{
    public TipoEquipoValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .MaximumLength(100);
    }
}
