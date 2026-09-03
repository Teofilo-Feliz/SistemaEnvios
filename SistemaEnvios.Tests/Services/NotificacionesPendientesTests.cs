using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Notificaciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Notificaciones;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// El aviso a Tecnología dice "hay un envío esperando en Transportación". En cuanto Tecnología
/// lo retira, deja de ser cierto y desaparece solo. Eso sustituye al acuse de lectura: el aviso
/// lo cierra el trabajo hecho, no un botón de "ya lo vi".
/// </summary>
public sealed class NotificacionesPendientesTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task SoloSeListanLosEnviosQueSiguenEnTransportacion()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarAsync(new ConsultarNotificacionesRequest { Rol = "TECNOLOGIA" });

        Assert.True(resultado.IsSuccess, resultado.Error);
        var aviso = Assert.Single(resultado.Value!.Items);
        Assert.Equal("ENV-ESPERANDO", aviso.NumeroEnvio);
    }

    [Fact]
    public async Task ElTotalTampocoCuentaLosYaRetirados()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarAsync(new ConsultarNotificacionesRequest { Rol = "TECNOLOGIA" });

        // Si el total contara los retirados, la campana marcaría trabajo que ya no existe.
        Assert.Equal(1, resultado.Value!.TotalItems);
    }

    private static NotificacionService Servicio(SistemaEnviosDbContext db)
    {
        var u = FakeUserContext.Global(UsuarioId);
        return new NotificacionService(db, u);
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var enTransportacion = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTransportacion, Nombre = "Recibido por Transportación", Activo = true };
        var yaRetirado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.RecibidoPorTecnologia, Nombre = "Recibido por Tecnología", Activo = true, EsFinal = true };
        db.AddRange(filial, tecnologia, enTransportacion, yaRetirado);
        await db.SaveChangesAsync();

        Envio Nuevo(string numero, EstadoEnvio estado) => new()
        {
            NumeroEnvio = numero,
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId
        };
        var esperando = Nuevo("ENV-ESPERANDO", enTransportacion);
        var retirado = Nuevo("ENV-RETIRADO", yaRetirado);
        db.Envios.AddRange(esperando, retirado);
        await db.SaveChangesAsync();

        // Los dos generaron su aviso en su momento; solo uno sigue siendo cierto.
        foreach (var envio in new[] { esperando, retirado })
            db.Notificaciones.Add(new Notificacion
            {
                EnvioId = envio.EnvioId,
                Tipo = "ENVIO_DISPONIBLE_TECNOLOGIA",
                Titulo = "Envío disponible para retiro",
                Mensaje = $"El envío {envio.NumeroEnvio} está disponible para ser retirado de Transportación.",
                DestinatarioRol = "TECNOLOGIA",
                FechaCreacion = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        return db;
    }
}
