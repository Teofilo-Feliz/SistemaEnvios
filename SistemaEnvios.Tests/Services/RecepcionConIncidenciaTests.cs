using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Recepciones;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Recibir conforme y recibir con incidencia son resultados distintos y el estado del envío
/// tiene que decirlo: quien mira la lista no debería abrir la recepción para enterarse de que
/// algo llegó mal.
///
/// Y es el cierre del caso lo que separa uno de otro: conforme cierra el ticket, con incidencia
/// lo deja abierto para la vuelta siguiente. El envío termina en los dos casos; el caso no.
/// </summary>
public sealed class RecepcionConIncidenciaTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task RecibirConformeDejaElEnvioEnRecibido()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);

        await CompletarAsync(db, e, EstadoRecepcionEquipoEnum.Verificado);

        var envio = await db.Envios.FirstAsync();
        Assert.Equal(e.Estados[EstadoEnvioCodigos.RecibidoEnFilial].EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task RecibirConIncidenciaDejaElEnvioEnRecibidoConIncidencia()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);

        await CompletarAsync(db, e, EstadoRecepcionEquipoEnum.VerificadoConIncidencia);

        var envio = await db.Envios.FirstAsync();
        Assert.Equal(e.Estados[EstadoEnvioCodigos.RecibidoEnFilialConIncidencia].EstadoEnvioId, envio.EstadoEnvioId);
    }

    [Fact]
    public async Task RecibirConformeCierraElCasoDelEquipo()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);

        await CompletarAsync(db, e, EstadoRecepcionEquipoEnum.Verificado);

        var apertura = await db.EnvioEquipos.FirstAsync(x => x.EnvioEquipoOrigenId == null);
        Assert.NotNull(apertura.FechaCierreCaso);
    }

    [Fact]
    public async Task RecibirConIncidenciaDejaElCasoAbiertoParaLaVueltaSiguiente()
    {
        await using var db = await SembrarAsync();
        var e = Datos(db);

        await CompletarAsync(db, e, EstadoRecepcionEquipoEnum.VerificadoConIncidencia);

        var apertura = await db.EnvioEquipos.FirstAsync(x => x.EnvioEquipoOrigenId == null);
        Assert.Null(apertura.FechaCierreCaso);
    }

    // ---------- apoyo ----------

    private sealed record Contexto(int EnvioId, int EnvioEquipoId, Dictionary<string, EstadoEnvio> Estados);

    private static Contexto Datos(SistemaEnviosDbContext db) => new(
        db.Envios.First().EnvioId,
        db.EnvioEquipos.First().EnvioEquipoId,
        db.EstadosEnvio.ToDictionary(x => x.Codigo));

    private static async Task CompletarAsync(SistemaEnviosDbContext db, Contexto e, EstadoRecepcionEquipoEnum estado)
    {
        var servicio = Servicio(db);
        var recepcionId = (await servicio.CrearAsync(new CrearRecepcionRequest { EnvioId = e.EnvioId })).Value;
        await servicio.VerificarEquipoAsync(new VerificarEquipoRequest
        {
            RecepcionId = recepcionId,
            EnvioEquipoId = e.EnvioEquipoId,
            Estado = estado,
            Observaciones = estado == EstadoRecepcionEquipoEnum.VerificadoConIncidencia ? "Llegó con la pantalla partida." : null,
        });
        var resultado = await servicio.CompletarAsync(recepcionId);
        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    private static RecepcionService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext u = FakeUserContext.Global(UsuarioId);
        var alcance = AlcanceDePrueba.Crear(db, u);
        return new RecepcionService(db, new UnitOfWork(db),
            new CrearRecepcionRequestValidator(), new VerificarEquipoRequestValidator(),
            new AsignarTecnicoRequestValidator(), u, alcance,
            new CasoEquipoService(db, new UnitOfWork(db), u, alcance));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var filial = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var recibido = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoEnFilial, Nombre = "Recibido en filial", Activo = true, EsFinal = true };
        var conIncidencia = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoEnFilialConIncidencia, Nombre = "Recibido en filial con incidencia", Activo = true, EsFinal = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        db.AddRange(tecnologia, filial, transito, recibido, conIncidencia, tipo);
        await db.SaveChangesAsync();

        db.TransicionesEstadoEnvio.AddRange(
            new TransicionEstadoEnvio { EstadoOrigenId = transito.EstadoEnvioId, EstadoDestinoId = recibido.EstadoEnvioId, Activo = true },
            new TransicionEstadoEnvio { EstadoOrigenId = transito.EstadoEnvioId, EstadoDestinoId = conIncidencia.EstadoEnvioId, Activo = true });

        var equipo = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = tecnologia.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-1" };
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-RECEPCION",
            UbicacionOrigenId = tecnologia.UbicacionId,
            UbicacionDestinoId = filial.UbicacionId,
            EstadoEnvioId = transito.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaFilial,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        db.EnvioEquipos.Add(new EnvioEquipo
        {
            EnvioId = envio.EnvioId,
            EquipoId = equipo.EquipoId,
            NumeroTicket = "800",
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Apertura del caso"
        });
        await db.SaveChangesAsync();
        return db;
    }
}
