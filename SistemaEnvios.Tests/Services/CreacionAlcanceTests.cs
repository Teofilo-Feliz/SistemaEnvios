using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Envios;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Un usuario de filial solo puede crear envíos de su propia filial. Sin esta regla podría
/// registrar un envío a nombre de otra filial y después ni siquiera verlo, porque el alcance
/// de lectura sí filtra.
/// </summary>
public sealed class CreacionAlcanceTests
{
    private static readonly Guid UsuarioId = Guid.Parse("1c6b4e29-70a5-4d38-9f12-6b8e0a3c5d47");
    private const string PosicionFilial = "Asistente Administrativo";

    [Fact]
    public async Task UsuarioDeFilial_NoPuedeCrearEnvioDeOtraFilial()
    {
        await using var db = await CrearContextoAsync();
        var ajena = await db.Ubicaciones.FirstAsync(x => x.CodigoCentro == "FIL-B");
        var tecnologia = await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Tecnologia);

        var resultado = await Servicio(db, "41,SANTIAGO").CrearAsync(new CrearEnvioRequest
        {
            UbicacionOrigenId = ajena.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
        });

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Forbidden, resultado.ErrorType);
    }

    [Fact]
    public async Task UsuarioDeFilial_PuedeCrearEnvioDeSuPropiaFilial()
    {
        await using var db = await CrearContextoAsync();
        var propia = await db.Ubicaciones.FirstAsync(x => x.FilialExternaId == 41);
        var tecnologia = await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Tecnologia);

        var resultado = await Servicio(db, "41,SANTIAGO").CrearAsync(new CrearEnvioRequest
        {
            UbicacionOrigenId = propia.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    [Fact]
    public async Task PerfilGlobal_PuedeCrearParaCualquierFilial()
    {
        await using var db = await CrearContextoAsync();
        var ajena = await db.Ubicaciones.FirstAsync(x => x.CodigoCentro == "FIL-B");
        var tecnologia = await db.Ubicaciones.FirstAsync(x => x.Tipo == TipoUbicacionEnum.Tecnologia);

        var usuario = FakeUserContext.Global(UsuarioId);
        var resultado = await Servicio(db, usuario).CrearAsync(new CrearEnvioRequest
        {
            UbicacionOrigenId = ajena.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db, string affiliate) =>
        Servicio(db, new FakeUserContext(UsuarioId, affiliate, position: PosicionFilial));

    private static EnvioService Servicio(SistemaEnviosDbContext db, IUserContext usuario) =>
        new(new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, new AlcanceEnvios(db, usuario), new CasoEquipoService(db, new UnitOfWork(db), usuario, new AlcanceEnvios(db, usuario)));

    private static async Task<SistemaEnviosDbContext> CrearContextoAsync()
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.AddRange(
            new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true },
            new Ubicacion { Nombre = "Santiago", CodigoCentro = "FIL-A", FilialExternaId = 41, Tipo = TipoUbicacionEnum.Filial, Activo = true },
            new Ubicacion { Nombre = "Azua", CodigoCentro = "FIL-B", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true },
            new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En filial", Activo = true },
            new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnPreparacionTecnologia, Nombre = "Preparación", Activo = true },
            new PerfilPosicion { Posicion = PosicionFilial, Perfil = (byte)PerfilAlcance.Filial });
        await db.SaveChangesAsync();
        return db;
    }
}
