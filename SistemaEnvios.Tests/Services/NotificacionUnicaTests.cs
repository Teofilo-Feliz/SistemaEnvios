using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El sistema avisa una sola vez: cuando Transportación confirma la llegada, Tecnología recibe
/// el aviso como quien recibe un correo. No hay acuse de lectura — un aviso que exige confirmar
/// que se leyó es trabajo administrativo que no mueve el envío.
/// </summary>
public sealed class NotificacionUnicaTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task ConfirmarLaLlegadaAvisaATecnologiaUnaSolaVez()
    {
        await using var db = await SembrarAsync();
        var envio = await db.Envios.FirstAsync();

        var resultado = await Servicio(db).ConfirmarLlegadaTransportacionAsync(envio.EnvioId);

        Assert.True(resultado.IsSuccess, resultado.Error);
        var aviso = Assert.Single(await db.Notificaciones.ToListAsync());
        Assert.Equal("TECNOLOGIA", aviso.DestinatarioRol);
        Assert.Contains(envio.NumeroEnvio, aviso.Mensaje);
    }

    [Fact]
    public async Task ElAvisoNoLlevaAcuseDeLectura()
    {
        await using var db = await SembrarAsync();
        var envio = await db.Envios.FirstAsync();
        await Servicio(db).ConfirmarLlegadaTransportacionAsync(envio.EnvioId);

        // La entidad no debe conservar rastro de lectura: si existiera la columna, alguien
        // volvería a construir el flujo de "marcar como leída" sobre ella.
        var propiedades = typeof(Notificacion).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("FechaLeida", propiedades);
        Assert.DoesNotContain("UsuarioLecturaId", propiedades);
    }

    // Confirmar la llegada es potestad de Transportación, no de un perfil global.
    private static EstadoEnvioService Servicio(SistemaEnviosDbContext db)
    {
        var u = new FakeUserContext(UsuarioId, "1,AZUA", position: "Encargado de Transportacion");
        return new EstadoEnvioService(db, new UnitOfWork(db), u, new AlcanceEnvios(db, u));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var recibido = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTransportacion, Nombre = "Recibido por Transportación", Activo = true };
        db.AddRange(filial, tecnologia, transito, recibido);
        db.Add(new PerfilPosicion { Posicion = "Encargado de Transportacion", Perfil = (byte)PerfilAlcance.Transportacion });
        await db.SaveChangesAsync();

        db.TransicionesEstadoEnvio.Add(new TransicionEstadoEnvio
        {
            EstadoOrigenId = transito.EstadoEnvioId,
            EstadoDestinoId = recibido.EstadoEnvioId,
            Activo = true
        });
        var tipo = new TipoTransporte { Codigo = "INTERNO", Nombre = "Interno", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        db.Add(tipo);
        var envio = new Envio
        {
            NumeroEnvio = "ENV-2026-AVISO",
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = transito.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        db.Envios.Add(envio);
        await db.SaveChangesAsync();

        // El guarda exige transporte institucional: es lo que hace que la llegada la confirme
        // Transportación y no cualquiera.
        db.Transportes.Add(new Transporte
        {
            EnvioId = envio.EnvioId,
            TipoTransporteId = tipo.TipoTransporteId,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacionId = UsuarioId
        });
        await db.SaveChangesAsync();
        return db;
    }
}
