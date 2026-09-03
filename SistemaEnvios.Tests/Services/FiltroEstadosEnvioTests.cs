using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Cada pantalla operativa mira un puñado de etapas concretas (Tecnología espera unas, la filial
/// otras). Sin este filtro tenían que pedir todos los envíos y descartarlos en el navegador.
/// </summary>
public sealed class FiltroEstadosEnvioTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8e4d2c19-6a35-4f70-b1c8-3d9f0a7e5b26");

    [Fact]
    public async Task DevuelveSoloLosEnviosEnLasEtapasPedidas()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            EstadoCodigos = [EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.RecibidoPorTransportacion]
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(5, resultado.Value!.TotalItems);
    }

    [Fact]
    public async Task SinEtapasPedidasNoFiltraNada()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest { EstadoCodigos = [] });

        Assert.Equal(8, resultado.Value!.TotalItems);
    }

    [Fact]
    public async Task ElFiltroDeEtapasSeCombinaConLaPaginacion()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            EstadoCodigos = [EstadoEnvioCodigos.EnTransito],
            PageSize = 2
        });

        Assert.Equal(2, resultado.Value!.Items.Count);
        Assert.Equal(3, resultado.Value.TotalItems);
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var usuario = FakeUserContext.Global(UsuarioId);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, new AlcanceEnvios(db, usuario));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var recibido = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTransportacion, Nombre = "Recibido por Transportación", Activo = true };
        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(filial, tecnologia, transito, recibido, enFilial);
        await db.SaveChangesAsync();

        Envio Nuevo(int i, EstadoEnvio estado) => new()
        {
            NumeroEnvio = $"ENV-{i:D4}",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
            FechaCreacion = DateTime.UtcNow.AddMinutes(-i)
        };
        db.Envios.AddRange(
            Nuevo(1, transito), Nuevo(2, transito), Nuevo(3, transito),
            Nuevo(4, recibido), Nuevo(5, recibido),
            Nuevo(6, enFilial), Nuevo(7, enFilial), Nuevo(8, enFilial));
        await db.SaveChangesAsync();
        return db;
    }
}
