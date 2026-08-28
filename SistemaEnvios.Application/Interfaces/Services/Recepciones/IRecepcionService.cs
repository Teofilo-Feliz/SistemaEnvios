using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Recepciones;
namespace SistemaEnvios.Application.Interfaces.Services;

public interface IRecepcionService
{
    Task<Result<int>> CrearAsync(CrearRecepcionRequest request, CancellationToken cancellationToken = default);
    Task<Result<RecepcionResponse>> ObtenerPorEnvioAsync(int envioId, CancellationToken cancellationToken = default);
    Task<Result> AsignarTecnicoAsync(AsignarTecnicoRequest request, CancellationToken cancellationToken = default);
    Task<Result> VerificarEquipoAsync(VerificarEquipoRequest request, CancellationToken cancellationToken = default);
    Task<Result> CompletarAsync(int recepcionId, CancellationToken cancellationToken = default);
}
