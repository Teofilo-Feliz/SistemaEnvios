using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Recepciones;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IRecepcionService
{
    Task<Result<int>> CrearAsync(CrearRecepcionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Equipos que llegaron mal en este envío, con lo que anotó quien los recibió.</summary>
    Task<Result<PaginaResponse<IncidenciaRecepcionResponse>>> ListarIncidenciasPorEnvioAsync(
        int envioId, ParametrosPaginaSimple request, CancellationToken cancellationToken = default);

    Task<Result<RecepcionResponse>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result> AsignarTecnicoAsync(AsignarTecnicoRequest request, CancellationToken cancellationToken = default);
    Task<Result> VerificarEquipoAsync(VerificarEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result> CompletarAsync(int recepcionId, CancellationToken cancellationToken = default);
}
