using FluentValidation;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;

namespace SistemaEnvios.Application.Validators.Envios;

public sealed class ActualizarEnvioEquipoRequestValidator : AbstractValidator<ActualizarEnvioEquipoRequest>
{
    public ActualizarEnvioEquipoRequestValidator()
    {
        RuleFor(x => x.EnvioEquipoId).GreaterThan(0);
        // Las mismas reglas que al agregar: antes aquí no había largo mínimo, así que un ticket
        // que no se podía crear sí se podía dejar editando el equipo.
        RuleFor(x => x.NumeroTicket)
            .Must(NumeroTicket.TieneFormato).WithMessage(NumeroTicket.MensajeFormato)
            .Must(NumeroTicket.EsMayorQueCero).WithMessage(NumeroTicket.MensajeMayorQueCero);
        RuleFor(x => x.Observaciones).NotEmpty().MaximumLength(2000);
    }
}
