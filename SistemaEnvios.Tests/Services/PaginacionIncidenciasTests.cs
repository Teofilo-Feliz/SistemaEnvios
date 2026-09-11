using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Incidencias;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Incidencias;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Incidencias;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// La pantalla de incidencias pedía todos los envíos y luego una consulta por cada uno. Un
/// listado propio y paginado la reduce a una petición, y el alcance debe sobrevivir el cambio:
/// una filial no puede ver las incidencias de otra.
/// </summary>
public sealed class PaginacionIncidenciasTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8e4d2c19-6a35-4f70-b1c8-3d9f0a7e5b26");

    [Fact]
    public async Task DevuelveUnaPaginaConElNumeroDeEnvioYaResuelto()
    {
        await using var db = await SembrarAsync(45);

        var resultado = await Servicio(db).ListarAsync(new ConsultarIncidenciasRequest { PageSize = 20 });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal(20, resultado.Value!.Items.Count);
        Assert.Equal(45, resultado.Value.TotalItems);
        Assert.All(resultado.Value.Items, x => Assert.StartsWith("ENV-", x.NumeroEnvio));
    }

    [Fact]
    public async Task UnUsuarioDeFilialSoloVeLasIncidenciasDeSusEnvios()
    {
        await using var db = await SembrarAsync(45);

        var resultado = await Servicio(db, "Asistente Administrativo").ListarAsync(new ConsultarIncidenciasRequest());

        Assert.Equal(22, resultado.Value!.TotalItems);
        Assert.All(resultado.Value.Items, x => Assert.Contains("SANTIAGO", x.Descripcion));
    }

    [Fact]
    public async Task PedirUnTamanoDesmedidoNoDevuelveTodasLasIncidencias()
    {
        await using var db = await SembrarAsync(150);

        var resultado = await Servicio(db).ListarAsync(new ConsultarIncidenciasRequest { PageSize = 100_000 });

        Assert.Equal(100, resultado.Value!.Items.Count);
        Assert.Equal(150, resultado.Value.TotalItems);
    }

    private static IncidenciaService Servicio(SistemaEnviosDbContext db, string posicion = "Programador Senior")
    {
        IUserContext usuario = new FakeUserContext(UsuarioId, "41,SANTIAGO", roles: [posicion]);
        return new IncidenciaService(
            db, new UnitOfWork(db), new CrearIncidenciaRequestValidator(), usuario, AlcanceDePrueba.Crear(db, usuario));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync(int cantidad)
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "FIL-A", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "FIL-B", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(santiago, azua, tecnologia, estado);
        db.AddRange(
            new PerfilPosicion { Posicion = "Asistente Administrativo", Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = (byte)PerfilAlcance.Global });
        await db.SaveChangesAsync();

        var envios = Enumerable.Range(1, cantidad).Select(i => new Envio
        {
            NumeroEnvio = $"ENV-{i:D4}",
            UbicacionOrigenId = i % 2 == 0 ? santiago.UbicacionId : azua.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
            FechaCreacion = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();
        db.Envios.AddRange(envios);
        await db.SaveChangesAsync();

        db.Incidencias.AddRange(envios.Select((envio, indice) => new Incidencia
        {
            EnvioId = envio.EnvioId,
            Descripcion = $"Novedad en {(indice % 2 == 0 ? "AZUA" : "SANTIAGO")} #{indice}",
            FechaCreacion = DateTime.UtcNow.AddMinutes(-indice),
            UsuarioCreacionId = UsuarioId
        }));
        await db.SaveChangesAsync();
        return db;
    }
}