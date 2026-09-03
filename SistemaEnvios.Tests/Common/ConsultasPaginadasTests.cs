using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Common;

/// <summary>
/// El corte tiene que ocurrir en la consulta, no después de traerla: si el Skip/Take se aplica
/// en memoria la base igual devolvió la tabla entera y la paginación es decorativa.
/// </summary>
public sealed class ConsultasPaginadasTests
{
    private sealed class Parametros : ParametrosPagina;

    [Fact]
    public async Task DevuelveSoloLaPaginaPedidaYElTotalCompleto()
    {
        await using var db = await SembrarAsync(25);

        var pagina = await db.Ubicaciones.AsNoTracking().OrderBy(x => x.Nombre)
            .PaginarAsync(new Parametros { Page = 2, PageSize = 10 }, x => x.Nombre, default);

        Assert.Equal(10, pagina.Items.Count);
        Assert.Equal(25, pagina.TotalItems);
        Assert.Equal(3, pagina.TotalPages);
        Assert.Equal("Filial 11", pagina.Items.First());
    }

    [Fact]
    public async Task UnTamanoDesmedidoNoTraeLaTablaCompleta()
    {
        await using var db = await SembrarAsync(150);

        var pagina = await db.Ubicaciones.AsNoTracking().OrderBy(x => x.UbicacionId)
            .PaginarAsync(new Parametros { PageSize = 100_000 }, x => x.Nombre, default);

        Assert.Equal(ParametrosPagina.TamanoMaximo, pagina.Items.Count);
        Assert.Equal(150, pagina.TotalItems);
    }

    [Fact]
    public async Task UnaPaginaMasAllaDelFinalVieneVaciaPeroConElTotalReal()
    {
        await using var db = await SembrarAsync(5);

        var pagina = await db.Ubicaciones.AsNoTracking().OrderBy(x => x.UbicacionId)
            .PaginarAsync(new Parametros { Page = 9, PageSize = 10 }, x => x.Nombre, default);

        Assert.Empty(pagina.Items);
        Assert.Equal(5, pagina.TotalItems);
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync(int cantidad)
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Ubicaciones.AddRange(Enumerable.Range(1, cantidad).Select(i => new Ubicacion
        {
            Nombre = $"Filial {i:D2}",
            CodigoCentro = $"C{i:D3}",
            Tipo = TipoUbicacionEnum.Filial,
            Activo = true
        }));
        await db.SaveChangesAsync();
        return db;
    }
}
