using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.DTOs.Equipos;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Equipos;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// La ficha del equipo mostraba una sola tarjeta con marca, modelo y serial: nada de por dónde
/// había pasado. Toda la trazabilidad que sostiene el modelo de casos —los viajes, el ticket que
/// se hereda de uno a otro— estaba en la base y no se podía ver desde ninguna pantalla.
///
/// El historial es por equipo, no por envío: un envío mueve varios equipos y cada uno tiene su
/// propia vida.
/// </summary>
public sealed class HistorialDeViajesTests
{
    private static readonly Guid UsuarioId = Guid.Parse("2c1f4b9e-7a3d-4a2f-9c8b-1e5d6f7a8b90");

    [Fact]
    public async Task DevuelveLosViajesDelEquipoYNoLosDeOtro()
    {
        await using var db = await SembrarAsync();
        var equipo = db.Equipos.Single(x => x.NumeroSerie == "SN-VIAJERO");

        var resultado = await Servicio(db).ListarViajesAsync(equipo.EquipoId, new ParametrosPaginaSimple());

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(2, resultado.Value!.Items.Count);
        Assert.DoesNotContain(resultado.Value.Items, x => x.NumeroEnvio == "ENV-DE-OTRO");
    }

    /// <summary>
    /// Lo más reciente primero: quien abre la ficha quiere saber dónde está el equipo ahora, no
    /// dónde estuvo la primera vez.
    /// </summary>
    [Fact]
    public async Task ElViajeMasRecienteVaPrimero()
    {
        await using var db = await SembrarAsync();
        var equipo = db.Equipos.Single(x => x.NumeroSerie == "SN-VIAJERO");

        var viajes = (await Servicio(db).ListarViajesAsync(equipo.EquipoId, new ParametrosPaginaSimple()))
            .Value!.Items.ToList();

        Assert.Equal("ENV-VUELTA", viajes[0].NumeroEnvio);
        Assert.Equal("ENV-IDA", viajes[1].NumeroEnvio);
    }

    /// <summary>
    /// El corazón de la trazabilidad: distinguir el viaje que estrenó el ticket del que lo
    /// heredó. Sin esto la ficha muestra dos veces el mismo número sin explicar por qué.
    /// </summary>
    [Fact]
    public async Task DistingueElViajeQueAbrioElCasoDelQueHeredoElTicket()
    {
        await using var db = await SembrarAsync();
        var equipo = db.Equipos.Single(x => x.NumeroSerie == "SN-VIAJERO");

        var viajes = (await Servicio(db).ListarViajesAsync(equipo.EquipoId, new ParametrosPaginaSimple())).Value!.Items;

        var vuelta = viajes.Single(x => x.NumeroEnvio == "ENV-VUELTA");
        var ida = viajes.Single(x => x.NumeroEnvio == "ENV-IDA");
        Assert.True(ida.EsAperturaDeCaso);
        Assert.False(vuelta.EsAperturaDeCaso);
        // Es el mismo caso, así que el ticket es el mismo en los dos viajes.
        Assert.Equal(ida.NumeroTicket, vuelta.NumeroTicket);
    }

    [Fact]
    public async Task TraeElRecorridoYElEstadoDeCadaViaje()
    {
        await using var db = await SembrarAsync();
        var equipo = db.Equipos.Single(x => x.NumeroSerie == "SN-VIAJERO");

        var ida = (await Servicio(db).ListarViajesAsync(equipo.EquipoId, new ParametrosPaginaSimple()))
            .Value!.Items.Single(x => x.NumeroEnvio == "ENV-IDA");

        Assert.Equal("Azua", ida.Origen);
        Assert.Equal("Tecnología", ida.Destino);
        Assert.Equal("Recibido en Tecnología", ida.EstadoNombre);
    }

    [Fact]
    public async Task UnEquipoSinViajesDevuelveListaVaciaYNoError()
    {
        await using var db = await SembrarAsync();
        var equipo = db.Equipos.Single(x => x.NumeroSerie == "SN-NUEVO");

        var resultado = await Servicio(db).ListarViajesAsync(equipo.EquipoId, new ParametrosPaginaSimple());

        // Un equipo recién registrado no es un error: todavía no se ha movido.
        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Empty(resultado.Value!.Items);
    }

    [Fact]
    public async Task NoDejaVerElHistorialDeUnEquipoFueraDeSuAlcance()
    {
        await using var db = await SembrarAsync();
        var ajeno = db.Equipos.Single(x => x.NumeroSerie == "SN-AJENO");

        // Usuario de Azua; el equipo vive en Santiago y nunca viajó en un envío suyo.
        var resultado = await Servicio(db, "1,AZUA", "Administrador de Filial")
            .ListarViajesAsync(ajeno.EquipoId, new ParametrosPaginaSimple());

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task ElEquipoQueNoExisteEsNotFoundYNoForbidden()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarViajesAsync(99999, new ParametrosPaginaSimple());

        // Confundir "no existe" con "no puede" deja al usuario buscando un permiso que no falta.
        Assert.Equal(ErrorType.NotFound, resultado.ErrorType);
    }

    private static EquipoService Servicio(
        SistemaEnviosDbContext db, string affiliate = "1,AZUA", string posicion = "Programador Senior")
    {
        IUserContext u = new FakeUserContext(UsuarioId, affiliate, position: posicion);
        return new EquipoService(db, new UnitOfWork(db),
            new CrearEquipoRequestValidator(), new ActualizarEquipoRequestValidator(),
            u, AlcanceDePrueba.Crear(db, u));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var recibido = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTecnologia, Nombre = "Recibido en Tecnología", Activo = true };
        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoEnFilial, Nombre = "Recibido en la filial", Activo = true, EsFinal = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(azua, santiago, tecnologia, recibido, enFilial, tipo);
        await db.SaveChangesAsync();

        var viajero = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-VIAJERO", CodigoActivo = "ADR-01" };
        var otro = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "HP", Modelo = "L2", NumeroSerie = "SN-OTRO", CodigoActivo = "ADR-02" };
        var nuevo = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = azua.UbicacionId, Marca = "Lenovo", Modelo = "L3", NumeroSerie = "SN-NUEVO", CodigoActivo = "ADR-03" };
        var ajeno = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = santiago.UbicacionId, Marca = "Acer", Modelo = "L4", NumeroSerie = "SN-AJENO", CodigoActivo = "ADR-04" };
        db.Equipos.AddRange(viajero, otro, nuevo, ajeno);
        await db.SaveChangesAsync();

        var ida = new Envio { NumeroEnvio = "ENV-IDA", UbicacionOrigenId = azua.UbicacionId, UbicacionDestinoId = tecnologia.UbicacionId, EstadoEnvioId = recibido.EstadoEnvioId, Direccion = DireccionEnvioEnum.HaciaTecnologia, UsuarioSolicitanteId = UsuarioId, FechaCreacion = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc) };
        var vuelta = new Envio { NumeroEnvio = "ENV-VUELTA", UbicacionOrigenId = tecnologia.UbicacionId, UbicacionDestinoId = azua.UbicacionId, EstadoEnvioId = enFilial.EstadoEnvioId, Direccion = DireccionEnvioEnum.HaciaFilial, UsuarioSolicitanteId = UsuarioId, FechaCreacion = new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc) };
        var deOtro = new Envio { NumeroEnvio = "ENV-DE-OTRO", UbicacionOrigenId = azua.UbicacionId, UbicacionDestinoId = tecnologia.UbicacionId, EstadoEnvioId = recibido.EstadoEnvioId, Direccion = DireccionEnvioEnum.HaciaTecnologia, UsuarioSolicitanteId = UsuarioId, FechaCreacion = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc) };
        db.Envios.AddRange(ida, vuelta, deOtro);
        await db.SaveChangesAsync();

        // La ida abre el caso con ticket propio; la vuelta lo continúa y hereda el mismo ticket.
        var aperturaIda = new EnvioEquipo { EnvioId = ida.EnvioId, EquipoId = viajero.EquipoId, NumeroTicket = "T-500", Observaciones = "Falla de pantalla", UsuarioSolicitanteId = UsuarioId, FechaCreacion = ida.FechaCreacion };
        db.EnvioEquipos.Add(aperturaIda);
        await db.SaveChangesAsync();

        db.EnvioEquipos.AddRange(
            new EnvioEquipo { EnvioId = vuelta.EnvioId, EquipoId = viajero.EquipoId, NumeroTicket = "T-500", Observaciones = "Pantalla reemplazada", EnvioEquipoOrigenId = aperturaIda.EnvioEquipoId, UsuarioSolicitanteId = UsuarioId, FechaCreacion = vuelta.FechaCreacion },
            new EnvioEquipo { EnvioId = deOtro.EnvioId, EquipoId = otro.EquipoId, NumeroTicket = "T-600", Observaciones = "No enciende", UsuarioSolicitanteId = UsuarioId, FechaCreacion = deOtro.FechaCreacion });
        await db.SaveChangesAsync();
        return db;
    }
}
