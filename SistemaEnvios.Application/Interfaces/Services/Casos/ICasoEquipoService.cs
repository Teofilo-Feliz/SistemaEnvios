using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Casos;
using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Application.Interfaces.Services;

public interface ICasoEquipoService
{
    /// <summary>Caso abierto del equipo, o null si no tiene: entonces el movimiento estrena ticket.</summary>
    Task<CasoAbiertoResponse?> BuscarAbiertoAsync(int equipoId, CancellationToken cancellationToken = default);

    /// <summary>Sella el cierre en el movimiento de apertura. Idempotente: gana el primer motivo.</summary>
    Task<Result> CerrarAsync(int envioEquipoAperturaId, string motivo, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caso abierto de un equipo, para que la pantalla de envío herede el ticket. A diferencia
    /// de BuscarAbiertoAsync —que es de uso interno— esta respeta el alcance, porque expone el
    /// ticket de una filial a quien lo consulta.
    /// </summary>
    Task<Result<CasoAbiertoResponse?>> ConsultarDeEquipoAsync(int equipoId, CancellationToken cancellationToken = default);

    /// <summary>Casos abiertos de equipos que hoy están en Tecnología: los candidatos a descarte.</summary>
    Task<Result<PaginaResponse<CasoListadoResponse>>> ListarAbiertosEnTecnologiaAsync(ConsultarCasosRequest request, CancellationToken cancellationToken = default);

    /// <summary>Descarta el equipo de su filial, cerrando el caso y dejándolo libre para reasignar.</summary>
    Task<Result> DescartarAsync(int equipoId, string motivo, CancellationToken cancellationToken = default);
}
