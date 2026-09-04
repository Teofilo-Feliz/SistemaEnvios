using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Saber que un envío llegó con incidencia no sirve de nada si no se puede ver cuál equipo vino
/// mal y qué se anotó. Ese dato se guardaba en la verificación de cada equipo y no lo exponía
/// ningún endpoint: había que mirarlo en la base.
/// </summary>
public sealed class IncidenciasDeRecepcionTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task DevuelveSoloLosEquiposQueVinieronMalConLoQueSeAnoto()
    {
        await using var db = await SembrarAsync();
        var envioId = db.Envios.First().EnvioId;

        var resultado = await Servicio(db).ListarIncidenciasPorEnvioAsync(envioId, new ParametrosPaginaSimple());

        Assert.True(resultado.IsSuccess, resultado.Error);
        var fila = Assert.Single(resultado.Value!.Items);
        Assert.Equal("SN-ROTO", fila.NumeroSerie);
        Assert.Equal("Llegó con la pantalla partida.", fila.Observaciones);
    }

    [Fact]
    public async Task ElEquipoConformeNoAparece()
    {
        await using var db = await SembrarAsync();
        var envioId = db.Envios.First().EnvioId;

        var resultado = await Servicio(db).ListarIncidenciasPorEnvioAsync(envioId, new ParametrosPaginaSimple());

        Assert.DoesNotContain(resultado.Value!.Items, x => x.NumeroSerie == "SN-BIEN");
    }

    [Fact]
    public async Task TraeElTicketParaPoderSeguirElCaso()
    {
        await using var db = await SembrarAsync();
        var envioId = db.Envios.First().EnvioId;

        var resultado = await Servicio(db).ListarIncidenciasPorEnvioAsync(envioId, new ParametrosPaginaSimple());

        Assert.Equal("800", resultado.Value!.Items.Single().NumeroTicket);
    }

    [Fact]
    public async Task RespetaElAlcanceDelEnvio()
    {
        await using var db = await SembrarAsync();
        var envioId = db.Envios.First().EnvioId;

        // Usuario de otra filial: el envío es de Azua (1) hacia Tecnología.
        var resultado = await Servicio(db, "41,SANTIAGO", "Administrador de Filial")
            .ListarIncidenciasPorEnvioAsync(envioId, new ParametrosPaginaSimple());

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    private static RecepcionService Servicio(
        SistemaEnviosDbContext db, string affiliate = "1,AZUA", string posicion = "Programador Senior")
    {
        IUserContext u = new FakeUserContext(UsuarioId, affiliate, position: posicion);
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

        var filial = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTecnologiaConIncidencia, Nombre = "Recibido con incidencia", Activo = true, EsFinal = true };
        var tipo = new TipoEquipo { Nombre = "Laptop", Activo = true };
        // Santiago existe y está mapeada: así el rechazo es por alcance (Forbidden) y no
        // por filial sin mapear (Conflict), que es un fallo distinto.
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        db.AddRange(filial, santiago, tecnologia, estado, tipo);
        await db.SaveChangesAsync();

        var roto = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = filial.UbicacionId, Marca = "Dell", Modelo = "L1", NumeroSerie = "SN-ROTO", CodigoActivo = "ADR-01" };
        var bien = new Equipo { TipoEquipoId = tipo.TipoEquipoId, UbicacionActualId = filial.UbicacionId, Marca = "Dell", Modelo = "L2", NumeroSerie = "SN-BIEN", CodigoActivo = "ADR-02" };
        db.Equipos.AddRange(roto, bien);
        await db.SaveChangesAsync();

        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-INCIDENCIA",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        var eeRoto = new EnvioEquipo { EnvioId = envio.EnvioId, EquipoId = roto.EquipoId, NumeroTicket = "800", UsuarioSolicitanteId = UsuarioId, Observaciones = "Equipo con falla" };
        var eeBien = new EnvioEquipo { EnvioId = envio.EnvioId, EquipoId = bien.EquipoId, NumeroTicket = "801", UsuarioSolicitanteId = UsuarioId, Observaciones = "Equipo de apoyo" };
        db.EnvioEquipos.AddRange(eeRoto, eeBien);
        await db.SaveChangesAsync();

        var recepcion = new Recepcion
        {
            EnvioId = envio.EnvioId,
            EstadoRecepcion = EstadoRecepcionEnum.CompletadaConIncidencia,
            UsuarioQueAsignoId = UsuarioId,
            FechaCreacion = DateTime.UtcNow
        };
        db.Recepciones.Add(recepcion);
        await db.SaveChangesAsync();

        db.RecepcionEquipos.AddRange(
            new RecepcionEquipo
            {
                RecepcionId = recepcion.RecepcionId,
                EnvioEquipoId = eeRoto.EnvioEquipoId,
                EstadoRecepcionEquipo = EstadoRecepcionEquipoEnum.VerificadoConIncidencia,
                Observaciones = "Llegó con la pantalla partida.",
                FechaVerificacion = DateTime.UtcNow
            },
            new RecepcionEquipo
            {
                RecepcionId = recepcion.RecepcionId,
                EnvioEquipoId = eeBien.EnvioEquipoId,
                EstadoRecepcionEquipo = EstadoRecepcionEquipoEnum.Verificado,
                FechaVerificacion = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        return db;
    }
}
