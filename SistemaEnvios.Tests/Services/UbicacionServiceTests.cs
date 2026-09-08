using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Ubicaciones;
using SistemaEnvios.Application.Validators.Ubicaciones;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Ubicaciones;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Los centros se administran desde una pantalla, así que las reglas que antes se sostenían
/// porque nadie podía tocarlos ahora hay que escribirlas: todo envío va entre una filial y
/// Tecnología, y esa relación se rompe de dos formas desde esta pantalla.
/// </summary>
public sealed class UbicacionServiceTests
{
    private static readonly Guid UsuarioId = Guid.Parse("5d8f2a71-4b0c-4e93-a6d5-91c3f7e08b24");

    [Fact]
    public async Task NoSePuedeDeshabilitarElUnicoCentroDeTecnologia()
    {
        // Sin Tecnología activa no se crea ni un envío en todo el sistema: DeterminarDireccion
        // solo admite filial->Tecnología o Tecnología->filial.
        await using var db = Contexto();
        var tecnologia = new Ubicacion
        {
            Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true,
        };
        db.Ubicaciones.Add(tecnologia);
        await db.SaveChangesAsync();

        var resultado = await Servicio(db).CambiarActivoAsync(tecnologia.UbicacionId, false);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        Assert.True((await db.Ubicaciones.FindAsync(tecnologia.UbicacionId))!.Activo);
    }

    [Fact]
    public async Task SePuedeDeshabilitarUnaTecnologiaSiQuedaOtraActiva()
    {
        await using var db = Contexto();
        var primera = new Ubicacion { Nombre = "Tec 1", CodigoCentro = "T1", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var segunda = new Ubicacion { Nombre = "Tec 2", CodigoCentro = "T2", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        db.Ubicaciones.AddRange(primera, segunda);
        await db.SaveChangesAsync();

        var resultado = await Servicio(db).CambiarActivoAsync(primera.UbicacionId, false);

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task SePuedeDeshabilitarUnaFilialAunqueSeaLaUnica()
    {
        // La restricción es solo para Tecnología: quedarse sin una filial concreta no impide
        // operar con las demás.
        await using var db = Contexto();
        var filial = new Ubicacion
        {
            Nombre = "Azua", CodigoCentro = "AZU", Tipo = TipoUbicacionEnum.Filial,
            FilialExternaId = 1, Activo = true,
        };
        db.Ubicaciones.Add(filial);
        await db.SaveChangesAsync();

        var resultado = await Servicio(db).CambiarActivoAsync(filial.UbicacionId, false);

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task NoSePuedeCambiarElTipoDeUnCentro()
    {
        // Tipo e id de AuthManager quedan fijos al crear: la dirección de los envíos se decidió
        // con ese tipo, y el id lo tiene denegado el usuario del API en la base.
        await using var db = Contexto();
        var (filial, tecnologia) = await SembrarConEnvioAsync(db);

        var resultado = await Servicio(db).ActualizarAsync(new GuardarUbicacionRequest
        {
            UbicacionId = filial.UbicacionId,
            Nombre = filial.Nombre,
            CodigoCentro = filial.CodigoCentro,
            Tipo = TipoUbicacionEnum.Tecnologia,
            FilialExternaId = null,
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
        var guardada = await db.Ubicaciones.FindAsync(filial.UbicacionId);
        Assert.Equal(TipoUbicacionEnum.Filial, guardada!.Tipo);
        Assert.Equal(1, guardada.FilialExternaId);
        Assert.NotNull(tecnologia);
    }

    [Fact]
    public async Task SePuedeRenombrarUnCentroConEnvios()
    {
        // Lo que se bloquea es el cambio de tipo, no la edición: corregir un nombre mal escrito
        // tiene que seguir siendo posible.
        await using var db = Contexto();
        var (filial, _) = await SembrarConEnvioAsync(db);

        var resultado = await Servicio(db).ActualizarAsync(new GuardarUbicacionRequest
        {
            UbicacionId = filial.UbicacionId,
            Nombre = "Azua Centro",
            CodigoCentro = filial.CodigoCentro,
            Tipo = TipoUbicacionEnum.Filial,
            FilialExternaId = 1,
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.Equal("Azua Centro", (await db.Ubicaciones.FindAsync(filial.UbicacionId))!.Nombre);
    }

    [Fact]
    public async Task NoSeAdmitenDosCentrosConElMismoIdDeAuthManager()
    {
        // Con dos, el filtro por filial buscaría una y encontraría dos: qué envíos ve el usuario
        // dependería del orden de la consulta.
        await using var db = Contexto();
        db.Ubicaciones.Add(new Ubicacion
        {
            Nombre = "Azua", CodigoCentro = "AZU", Tipo = TipoUbicacionEnum.Filial,
            FilialExternaId = 30, Activo = true,
        });
        await db.SaveChangesAsync();

        var resultado = await Servicio(db).CrearAsync(new GuardarUbicacionRequest
        {
            Nombre = "Otra", CodigoCentro = "OTR", Tipo = TipoUbicacionEnum.Filial, FilialExternaId = 30,
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    private static async Task<(Ubicacion Filial, Ubicacion Tecnologia)> SembrarConEnvioAsync(
        SistemaEnviosDbContext db)
    {
        var filial = new Ubicacion
        {
            Nombre = "Azua", CodigoCentro = "AZU", Tipo = TipoUbicacionEnum.Filial,
            FilialExternaId = 1, Activo = true,
        };
        var tecnologia = new Ubicacion
        {
            Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true,
        };
        var estado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        db.AddRange(filial, tecnologia, estado);
        await db.SaveChangesAsync();

        db.Envios.Add(new Envio
        {
            NumeroEnvio = Guid.NewGuid().ToString("N"),
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
        });
        await db.SaveChangesAsync();
        return (filial, tecnologia);
    }

    private static UbicacionService Servicio(SistemaEnviosDbContext db) =>
        new(db, new UnitOfWork(db), new GuardarUbicacionRequestValidator(), FakeUserContext.Global(UsuarioId));

    private static SistemaEnviosDbContext Contexto() =>
        new(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
