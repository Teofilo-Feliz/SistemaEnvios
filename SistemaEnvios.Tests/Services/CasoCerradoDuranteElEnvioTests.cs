using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Services.Integraciones;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// La ventana entre leer el caso de un equipo y guardar el envío.
///
/// No es un instante: en medio va la consulta a GLPI, que admite hasta diez segundos. Si
/// Tecnología descarta el equipo en ese rato, el envío se guardaría heredando el ticket de un
/// caso ya cerrado. Aquí el descarte se provoca justo durante esa consulta.
/// </summary>
public sealed class CasoCerradoDuranteElEnvioTests
{
    private static readonly Guid UsuarioId = Guid.Parse("2b4f1a09-5c7e-4d18-9f30-6a1e8c52d743");

    [Fact]
    public async Task SiElCasoSeCierraMientrasSeConsultaGlpi_ElEnvioNoSeCrea()
    {
        var baseDatos = Guid.NewGuid().ToString();
        await using var db = await SembrarAsync(baseDatos);
        var equipoId = db.Equipos.First().EquipoId;

        var resultado = await Servicio(db, new GlpiQueDescarta(baseDatos))
            .CrearConEquiposAsync(Peticion(db, equipoId));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        Assert.Contains("se cerró mientras se preparaba", resultado.Error);
    }

    [Fact]
    public async Task SiNadieToca_ElCaso_ElEnvioSeCrea()
    {
        var baseDatos = Guid.NewGuid().ToString();
        await using var db = await SembrarAsync(baseDatos);
        var equipoId = db.Equipos.First().EquipoId;

        var resultado = await Servicio(db, new ValidadorTicketDePrueba())
            .CrearConEquiposAsync(Peticion(db, equipoId));

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    /// <summary>El envío no llega a existir: el guardia corre antes de guardar.</summary>
    [Fact]
    public async Task ElRechazoNoDejaRastro()
    {
        var baseDatos = Guid.NewGuid().ToString();
        await using var db = await SembrarAsync(baseDatos);
        var equipoId = db.Equipos.First().EquipoId;
        var enviosAntes = db.Envios.Count();

        await Servicio(db, new GlpiQueDescarta(baseDatos)).CrearConEquiposAsync(Peticion(db, equipoId));

        await using var comprobacion = Contexto(baseDatos);
        Assert.Equal(enviosAntes, comprobacion.Envios.Count());
        Assert.Empty(comprobacion.ReservasEquipoEnvio);
    }

    private static CrearEnvioConEquiposRequest Peticion(SistemaEnviosDbContext db, int equipoId)
    {
        var filial = db.Ubicaciones.First(x => x.Tipo == TipoUbicacionEnum.Filial);
        var tecnologia = db.Ubicaciones.First(x => x.Tipo == TipoUbicacionEnum.Tecnologia);
        return new CrearEnvioConEquiposRequest
        {
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            Observaciones = "Reenvío del mismo caso",
            Equipos = [new EquipoEnvioInicialRequest { EquipoId = equipoId, NumeroTicket = "830001" }],
        };
    }

    /// <summary>
    /// Cierra el caso del equipo mientras el servicio cree que está hablando con la mesa de
    /// ayuda. Es el momento exacto en que hoy cabe un descarte.
    /// </summary>
    private sealed class GlpiQueDescarta(string baseDatos) : IValidadorTicketGlpi
    {
        public Task<Result> ValidarAsync(string numeroTicket, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> ValidarParaEquipoAsync(string numeroTicket, string? numeroSerie, CancellationToken ct = default) =>
            Task.FromResult(Result.Success());

        public async Task<Result> ValidarVariosAsync(IReadOnlyCollection<string> numerosTicket, CancellationToken ct = default)
        {
            await using var otra = Contexto(baseDatos);
            var apertura = await otra.EnvioEquipos.FirstAsync(x => x.EnvioEquipoOrigenId == null, ct);
            apertura.FechaCierreCaso = DateTime.UtcNow;
            apertura.MotivoCierreCaso = "Descartado de la filial: prueba";
            await otra.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db, IValidadorTicketGlpi validador)
    {
        var usuario = FakeUserContext.Global(UsuarioId);
        var alcance = AlcanceDePrueba.Crear(db, usuario);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, alcance,
            new CasoEquipoService(db, new UnitOfWork(db), usuario, alcance),
            validador);
    }

    private static SistemaEnviosDbContext Contexto(string baseDatos) =>
        new(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(baseDatos)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<SistemaEnviosDbContext> SembrarAsync(string baseDatos)
    {
        var db = Contexto(baseDatos);
        var filial = new Ubicacion { Nombre = "Filial", CodigoCentro = "FIL", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(filial, tecnologia, tipo, estado);
        await db.SaveChangesAsync();

        var equipo = new Equipo
        {
            TipoEquipoId = tipo.TipoEquipoId,
            UbicacionActualId = filial.UbicacionId,
            Marca = "Dell",
            Modelo = "Latitude",
            NumeroSerie = "SN-CASO",
        };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();

        var previo = new Envio
        {
            NumeroEnvio = "ENV-2026-PREVIO",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
        };
        db.Envios.Add(previo);
        await db.SaveChangesAsync();

        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = previo.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "830001",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Apertura del caso",
        });
        await db.SaveChangesAsync();
        return db;
    }
}
