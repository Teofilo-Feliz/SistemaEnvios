using SistemaEnvios.Api.Controllers.Catalogos;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Tests.Api;

/// <summary>
/// El controlador reconstruye el request para que mande el id de la ruta y no el del cuerpo. Al
/// añadir FilialExternaId se olvidó copiarlo ahí, y como el validador lo exige para una filial,
/// editar cualquier filial devolvía 400 para siempre. Las pruebas del servicio no lo veían
/// porque lo llaman directamente y se saltan el controlador; esta entra por donde entra el
/// usuario.
/// </summary>
public sealed class UbicacionesControllerTests
{
    [Fact]
    public async Task ActualizarLeLlegaAlServicioConElIdDeAuthManager()
    {
        var espia = new ServicioEspia();
        var controlador = new UbicacionesController(espia);

        await controlador.Actualizar(
            ubicacionId: 7,
            new GuardarUbicacionRequest
            {
                UbicacionId = 999, // el del cuerpo debe ignorarse
                Nombre = "Santo Domingo",
                CodigoCentro = "SDQ",
                Tipo = TipoUbicacionEnum.Filial,
                FilialExternaId = 30,
            },
            CancellationToken.None);

        Assert.NotNull(espia.Recibido);
        Assert.Equal(30, espia.Recibido!.FilialExternaId);
        // El id de la ruta manda sobre el del cuerpo: es la razón de reconstruir el request.
        Assert.Equal(7, espia.Recibido.UbicacionId);
        Assert.Equal("Santo Domingo", espia.Recibido.Nombre);
        Assert.Equal("SDQ", espia.Recibido.CodigoCentro);
        Assert.Equal(TipoUbicacionEnum.Filial, espia.Recibido.Tipo);
    }

    private sealed class ServicioEspia : IUbicacionService
    {
        public GuardarUbicacionRequest? Recibido { get; private set; }

        public Task<Result> ActualizarAsync(GuardarUbicacionRequest request, CancellationToken ct = default)
        {
            Recibido = request;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<int>> CrearAsync(GuardarUbicacionRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Success(1));

        public Task<Result> CambiarActivoAsync(int ubicacionId, bool activo, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result<UbicacionResponse>> ObtenerAsync(int ubicacionId, CancellationToken ct = default) =>
            Task.FromResult(Result<UbicacionResponse>.Success(
                new UbicacionResponse(ubicacionId, "x", "x", TipoUbicacionEnum.Filial, true, 1)));

        public Task<Result<PaginaResponse<UbicacionResponse>>> ListarAsync(
            ConsultarCatalogoRequest request, CancellationToken ct = default) =>
            Task.FromResult(Result<PaginaResponse<UbicacionResponse>>.Success(
                new PaginaResponse<UbicacionResponse>([], 1, 20, 0, 0)));
    }
}
