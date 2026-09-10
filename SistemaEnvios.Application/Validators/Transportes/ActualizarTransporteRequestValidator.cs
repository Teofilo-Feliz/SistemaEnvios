using FluentValidation;
using SistemaEnvios.Application.DTOs.Transportes;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Application.Validators.Transportes;

public sealed class ActualizarTransporteRequestValidator : AbstractValidator<ActualizarTransporteRequest>
{
    public ActualizarTransporteRequestValidator()
    {
        RuleFor(x => x.TransporteId).GreaterThan(0);
        RuleFor(x => x.TipoTransporteId).GreaterThan(0);
        RuleFor(x => x.NombreResponsable).MaximumLength(150);
        RuleFor(x => x.Parentesco).MaximumLength(50);
        // El formato depende del tipo: la cédula no admite letras, el pasaporte sí. La regla vive
        // en FormatosDocumento para que el CHECK de la tabla y esta validacion no se separen.
        RuleFor(x => x.DocumentoResponsable)
            .Must((peticion, valor) => FormatosDocumento.EsValido(peticion.TipoDocumento ?? TipoDocumentoEnum.Cedula, valor))
            .WithMessage(peticion => (peticion.TipoDocumento ?? TipoDocumentoEnum.Cedula) == TipoDocumentoEnum.Pasaporte
                ? "El pasaporte debe tener entre 6 y 15 letras o números, sin espacios ni guiones."
                : "La cédula debe tener exactamente 11 dígitos, sin letras.")
            .When(x => !string.IsNullOrWhiteSpace(x.DocumentoResponsable));
        RuleFor(x => x.PlacaVehiculo).MaximumLength(20);
        RuleFor(x => x.Observaciones).MaximumLength(2000);
    }
}
