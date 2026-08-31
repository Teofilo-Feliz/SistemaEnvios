using FluentValidation;
using SistemaEnvios.Application.DTOs.Recepciones;
namespace SistemaEnvios.Application.Validators.Recepciones;

public sealed class CrearRecepcionRequestValidator : AbstractValidator<CrearRecepcionRequest>
{
    public CrearRecepcionRequestValidator()
    {
        RuleFor(x => x.EnvioId).GreaterThan(0);
        RuleFor(x => x.TecnicoAsignadoUsuarioId).NotEqual(Guid.Empty).When(x => x.TecnicoAsignadoUsuarioId.HasValue);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
