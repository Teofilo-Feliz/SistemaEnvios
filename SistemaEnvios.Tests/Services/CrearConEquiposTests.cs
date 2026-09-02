using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Crear un envío con equipos es una sola operación: o queda todo, o no queda nada.
/// Antes se guardaba el envío primero y los equipos después, así que un fallo en el segundo
/// paso dejaba un envío vacío, y un reintento transitorio podía duplicarlo.
/// </summary>
public sealed class CrearConEquiposTests
{
    private static readonly Guid UsuarioId = Guid.Parse("5d8f2a71-4b0c-4e93-a6d5-91c3f7e08b24");

    [Fact]
    public async Task ConEquipoInvalido_NoDejaEnvioAMedias()
    {
        await using var db = await SembrarAsync(db => { });
        var (filial, tecnologia) = await UbicacionesAsync(db);

        var resultado = await Servicio(db).CrearConEquiposAsync(new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            Equipos = [new() { EquipoId = 9999, NumeroTicket = "123" }],
        });

        Assert.True(resultado.IsFailure);
        Assert.Empty(await db.Envios.ToListAsync());
        Assert.Empty(await db.HistorialEstadosEnvio.ToListAsync());
    }

    [Fact]
    public async Task ConEquiposValidos_CreaEnvioEquiposYReservasUnaSolaVez()
    {
        await using var db = await SembrarAsync(db => { });
        var (filial, tecnologia) = await UbicacionesAsync(db);
        var equipo = await db.Equipos.FirstAsync();

        var resultado = await Servicio(db).CrearConEquiposAsync(new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            Equipos = [new() { EquipoId = equipo.EquipoId, NumeroTicket = "1234" }],
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Single(await db.Envios.ToListAsync());
        Assert.Single(await db.EnvioEquipos.ToListAsync());
        Assert.Single(await db.ReservasEquipoEnvio.ToListAsync());
        Assert.Single(await db.HistorialEstadosEnvio.ToListAsync());
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var usuario = FakeUserContext.Global(UsuarioId);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, new AlcanceEnvios(db, usuario));
    }

    private static async Task<(Ubicacion Filial, Ubicacion Tecnologia)> UbicacionesAsync(SistemaEnviosDbContext db) =>
        (await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Filial),
         await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Tecnologia));

    private static async Task<SistemaEnviosDbContext> SembrarAsync(Action<SistemaEnviosDbContext> extra)
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(filial, tecnologia, tipo);
        db.EstadosEnvio.Add(new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true });
        await db.SaveChangesAsync();

        db.Equipos.Add(new Equipo
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = filial.UbicacionId,
            Marca = "Dell",
            Modelo = "Latitude",
            NumeroSerie = "SN-1",
            CodigoActivo = "ADR-1",
        });
        extra(db);
        await db.SaveChangesAsync();
        return db;
    }
}
