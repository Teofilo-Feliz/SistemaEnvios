using FluentValidation;
using SistemaEnvios.Application.DTOs.Recepciones;

namespace SistemaEnvios.Application.Validators.Recepciones;

public sealed class AsignarTecnicoRequestValidator : AbstractValidator<AsignarTecnicoRequest>
{
    public AsignarTecnicoRequestValidator()
    {
        RuleFor(x => x.RecepcionId).GreaterThan(0);
        RuleFor(x => x.TecnicoAsignadoUsuarioId).NotEqual(Guid.Empty);
    }
}
