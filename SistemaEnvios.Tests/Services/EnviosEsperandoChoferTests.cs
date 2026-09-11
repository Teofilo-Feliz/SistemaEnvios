using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Cuando Tecnología despacha hacia una filial, el envío queda en EN_TRANSPORTACION sin
/// transporte todavía: el transporte nace justo cuando Transportación le asigna el chofer. El
/// propio despacho lo deja anotado —"Transportación recibió el envío y debe asignarle chofer"—.
///
/// El alcance exigía que el envío ya tuviera transporte, así que Transportación no veía
/// precisamente los envíos que le tocaba atender: no podía verlo porque no tenía transporte, y
/// no podía tener transporte porque ella es quien lo asigna.
///
/// La condición existía para que el transporte privado no le apareciera. Eso se conserva por
/// otra vía: el flujo privado exige un transporte válido antes de moverse, así que "sin
/// transporte" nunca significa privado, significa "todavía sin asignar".
/// </summary>
public sealed class EnviosEsperandoChoferTests
{
    private static readonly Guid UsuarioId = Guid.Parse("7b4a1d62-9c3e-4f81-a5d0-2e6b8c9f0a13");

    [Fact]
    public async Task VeElEnvioQueTecnologiaDespachoYEsperaChofer()
    {
        await using var db = await SembrarAsync();

        var numeros = await VisiblesAsync(db);

        Assert.Contains("ENV-ESPERA-CHOFER", numeros);
    }

    [Fact]
    public async Task SigueViendoLosQueYaLlevanTransporteInstitucional()
    {
        await using var db = await SembrarAsync();

        Assert.Contains("ENV-INSTITUCIONAL", await VisiblesAsync(db));
    }

    /// <summary>
    /// La razón por la que existía la condición que causó el fallo. Si esto se rompe,
    /// Transportación empieza a ver envíos que van directo a Tecnología sin pasar por ella.
    /// </summary>
    [Fact]
    public async Task NoVeElEnvioDeTransportePrivado()
    {
        await using var db = await SembrarAsync();

        Assert.DoesNotContain("ENV-PRIVADO", await VisiblesAsync(db));
    }

    [Fact]
    public async Task NoVeUnEnvioEnUnaEtapaQueNoLeToca()
    {
        await using var db = await SembrarAsync();

        Assert.DoesNotContain("ENV-EN-FILIAL", await VisiblesAsync(db));
    }

    private static async Task<List<string>> VisiblesAsync(SistemaEnviosDbContext db)
    {
        IUserContext usuario = new FakeUserContext(UsuarioId, "30,SANTO DOMINGO (SEDE)", roles: ["Encargado Transportación"]);
        var alcance = AlcanceDePrueba.Crear(db, usuario);
        var query = await alcance.FiltrarAsync(db.Envios.AsNoTracking());
        return await query.Select(x => x.NumeroEnvio).ToListAsync();
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "San Francisco de Macorís", CodigoCentro = "F27", FilialExternaId = 27, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var enTransportacion = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransportacion, Nombre = "En Transportación", Activo = true };
        var enTransito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var enFilial = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnFilial, Nombre = "En la filial", Activo = true };
        var institucional = new TipoTransporte { Codigo = "INST", Nombre = "Transportación institucional", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        var privado = new TipoTransporte { Codigo = "PRIV", Nombre = "Transporte privado", Estrategia = EstrategiaTransporteEnum.EntregaDirectaTecnologia, Activo = true };
        db.AddRange(filial, tecnologia, enTransportacion, enTransito, enFilial, institucional, privado);
        await db.SaveChangesAsync();

        var espera = Envio("ENV-ESPERA-CHOFER", tecnologia, filial, enTransportacion, DireccionEnvioEnum.HaciaFilial);
        var conInstitucional = Envio("ENV-INSTITUCIONAL", tecnologia, filial, enTransportacion, DireccionEnvioEnum.HaciaFilial);
        var conPrivado = Envio("ENV-PRIVADO", filial, tecnologia, enTransito, DireccionEnvioEnum.HaciaTecnologia);
        var sinSalir = Envio("ENV-EN-FILIAL", filial, tecnologia, enFilial, DireccionEnvioEnum.HaciaTecnologia);
        db.Envios.AddRange(espera, conInstitucional, conPrivado, sinSalir);
        await db.SaveChangesAsync();

        db.Transportes.AddRange(
            new Transporte { EnvioId = conInstitucional.EnvioId, TipoTransporteId = institucional.TipoTransporteId },
            new Transporte { EnvioId = conPrivado.EnvioId, TipoTransporteId = privado.TipoTransporteId });
        await db.SaveChangesAsync();
        return db;
    }

    private static Envio Envio(string numero, Ubicacion origen, Ubicacion destino, EstadoEnvio estado, DireccionEnvioEnum direccion) =>
        new()
        {
            NumeroEnvio = numero,
            UbicacionOrigenId = origen.UbicacionId,
            UbicacionDestinoId = destino.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = direccion,
            UsuarioSolicitanteId = UsuarioId
        };
}