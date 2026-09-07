using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Un envío lo edita quien lo tiene en la mano, y solo mientras no se haya movido. La filial
/// edita lo suyo en EN_FILIAL; lo que Tecnología prepara es de Tecnología aunque el destino sea
/// esa filial, y por eso aparezca en su listado.
/// </summary>
public sealed class EdicionPorCustodiaTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task LaFilialEditaSuPropioEnvioMientrasNoHaSalido()
    {
        await using var db = await SembrarAsync();
        var envio = await Envio(db, "ENV-DE-AZUA");

        var resultado = await Servicio(db, "Asistente Administrativo").ActualizarAsync(Cambio(db, envio));

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task LaFilialNoEditaLoQueTecnologiaEstaPreparandoParaElla()
    {
        await using var db = await SembrarAsync();
        // Va dirigido a Azua, así que la filial lo ve en su listado; pero es de Tecnología.
        var envio = await Envio(db, "ENV-PREPARA-TEC");

        var resultado = await Servicio(db, "Asistente Administrativo").ActualizarAsync(Cambio(db, envio));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task TecnologiaSiEditaLoQuePrepara()
    {
        await using var db = await SembrarAsync();
        var envio = await Envio(db, "ENV-PREPARA-TEC");

        var resultado = await Servicio(db, "Programador Senior").ActualizarAsync(Cambio(db, envio));

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task NadieEditaElEnvioDeUnaFilialAjena()
    {
        await using var db = await SembrarAsync();
        var envio = await Envio(db, "ENV-DE-SANTIAGO");

        // El usuario es de Azua y el envío es de Santiago: fuera de su alcance.
        var resultado = await Servicio(db, "Asistente Administrativo").ActualizarAsync(Cambio(db, envio));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task UnEnvioQueYaSalioNoSeEditaNiPorSuPropiaFilial()
    {
        await using var db = await SembrarAsync();
        var envio = await Envio(db, "ENV-YA-SALIO");

        var resultado = await Servicio(db, "Asistente Administrativo").ActualizarAsync(Cambio(db, envio));

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.ErrorType);
    }

    // ---------- apoyo ----------

    private static async Task<Envio> Envio(SistemaEnviosDbContext db, string numero) =>
        await db.Envios.AsNoTracking().FirstAsync(x => x.NumeroEnvio == numero);

    private static ActualizarEnvioRequest Cambio(SistemaEnviosDbContext db, Envio envio) => new()
    {
        EnvioId = envio.EnvioId,
        UbicacionOrigenId = envio.UbicacionOrigenId,
        UbicacionDestinoId = envio.UbicacionDestinoId,
        Observaciones = "Observaciones corregidas.",
    };

    private static EnvioService Servicio(SistemaEnviosDbContext db, string posicion)
    {
        IUserContext u = new FakeUserContext(UsuarioId, "1,AZUA", position: posicion);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            u, AlcanceDePrueba.Crear(db, u),
            new CasoEquipoService(db, new UnitOfWork(db), u, AlcanceDePrueba.Crear(db, u)), ValidadorTicketDePrueba.QueAcepta());
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var azua = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var santiago = new Ubicacion { Nombre = "Santiago", CodigoCentro = "F41", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true };
        var preparacion = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnPreparacionTecnologia, Nombre = "En preparación", Activo = true };
        var entregado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EntregadoATransportacion, Nombre = "Entregado", Activo = true };
        db.AddRange(azua, santiago, tecnologia, enFilial, preparacion, entregado);
        db.AddRange(
            new PerfilPosicion { Posicion = "Asistente Administrativo", Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = "Programador Senior", Perfil = (byte)PerfilAlcance.Global });
        await db.SaveChangesAsync();

        Envio Nuevo(string numero, int origen, int destino, EstadoEnvio estado, DireccionEnvioEnum direccion) => new()
        {
            NumeroEnvio = numero,
            UbicacionOrigenId = origen,
            UbicacionDestinoId = destino,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = UsuarioId,
            Observaciones = "Original"
        };
        db.Envios.AddRange(
            Nuevo("ENV-DE-AZUA", azua.UbicacionId, tecnologia.UbicacionId, enFilial, DireccionEnvioEnum.HaciaTecnologia),
            Nuevo("ENV-PREPARA-TEC", tecnologia.UbicacionId, azua.UbicacionId, preparacion, DireccionEnvioEnum.HaciaFilial),
            Nuevo("ENV-DE-SANTIAGO", santiago.UbicacionId, tecnologia.UbicacionId, enFilial, DireccionEnvioEnum.HaciaTecnologia),
            Nuevo("ENV-YA-SALIO", azua.UbicacionId, tecnologia.UbicacionId, entregado, DireccionEnvioEnum.HaciaTecnologia));
        await db.SaveChangesAsync();
        return db;
    }
}
