using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.DTOs.Envios;
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
/// El transporte privado va de la filial a Tecnología sin pasar por Transportación, así que no
/// debe aparecer en su módulo. Esa regla vivía solo en AlcanceEnvios, es decir solo para quien
/// tiene el perfil Transportacion: un usuario Global o de Tecnología abría el mismo módulo y veía
/// los privados, porque las pantallas solo filtraban por etapa.
///
/// El caso real que lo destapó: un envío privado puesto en tránsito aparecía en Transportación y
/// no aparecía en ninguna pantalla de Tecnología, que es quien tiene que recibirlo.
/// </summary>
public sealed class FiltroEstrategiaTransporteTests
{
    private static readonly Guid UsuarioId = Guid.Parse("2f7c8a41-95b3-4e02-8d6a-1c5b7e094f38");

    [Fact]
    public async Task TransportacionNoVeLosEnviosPrivados()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            EstadoCodigos = EstadoEnvioCodigos.EtapasTransportacion,
            ExcluirEstrategiaTransporte = EstrategiaTransporteEnum.EntregaDirectaTecnologia
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        Assert.DoesNotContain(resultado.Value!.Items, x => x.NumeroEnvio == "ENV-PRIVADO");
    }

    /// <summary>
    /// El envío que Tecnología acaba de despachar todavía no tiene transporte —nace cuando
    /// Transportación le asigna chofer— y es justo el que ella tiene que atender. Por eso el
    /// filtro es "no privado" y no "solo institucional".
    /// </summary>
    [Fact]
    public async Task TransportacionSigueViendoLoInstitucionalYLoQueAunNoTieneTransporte()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            EstadoCodigos = EstadoEnvioCodigos.EtapasTransportacion,
            ExcluirEstrategiaTransporte = EstrategiaTransporteEnum.EntregaDirectaTecnologia
        });

        Assert.Contains(resultado.Value!.Items, x => x.NumeroEnvio == "ENV-INSTITUCIONAL");
        Assert.Contains(resultado.Value.Items, x => x.NumeroEnvio == "ENV-SIN-TRANSPORTE");
        Assert.Equal(2, resultado.Value.TotalItems);
    }

    [Fact]
    public async Task TecnologiaVeSoloLosPrivadosEnTransito()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            EstadoCodigos = [EstadoEnvioCodigos.EnTransito],
            EstrategiaTransporte = EstrategiaTransporteEnum.EntregaDirectaTecnologia
        });

        Assert.True(resultado.IsSuccess, resultado.Error);
        var unico = Assert.Single(resultado.Value!.Items);
        Assert.Equal("ENV-PRIVADO", unico.NumeroEnvio);
    }

    /// <summary>Sin pedir estrategia, la consulta se comporta como antes.</summary>
    [Fact]
    public async Task SinFiltroDeEstrategiaDevuelveTodo()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ConsultarAsync(new ConsultarEnviosRequest
        {
            EstadoCodigos = EstadoEnvioCodigos.EtapasTransportacion
        });

        Assert.Equal(3, resultado.Value!.TotalItems);
    }

    private static EnvioService Servicio(SistemaEnviosDbContext db)
    {
        var usuario = FakeUserContext.Global(UsuarioId);
        return new EnvioService(
            new GenericRepository<Envio>(db), db, new UnitOfWork(db),
            new CrearEnvioRequestValidator(), new ActualizarEnvioRequestValidator(),
            usuario, AlcanceDePrueba.Crear(db, usuario),
            new CasoEquipoService(db, new UnitOfWork(db), usuario, AlcanceDePrueba.Crear(db, usuario)),
            ValidadorTicketDePrueba.QueAcepta());
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var filial = new Ubicacion { Nombre = "Baní", CodigoCentro = "BANI", FilialExternaId = 2, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TECNOLOGIA", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var enTransportacion = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransportacion, Nombre = "En espera de asignación de chofer", Activo = true };
        var interno = new TipoTransporte { Codigo = "INTERNO", Nombre = "Interno", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        var privado = new TipoTransporte { Codigo = "PRIVADO", Nombre = "Privado", Estrategia = EstrategiaTransporteEnum.EntregaDirectaTecnologia, Activo = true };
        db.AddRange(filial, tecnologia, transito, enTransportacion, interno, privado);
        await db.SaveChangesAsync();

        Envio Nuevo(string numero, EstadoEnvio estado) => new()
        {
            NumeroEnvio = numero,
            UbicacionOrigenId = filial.UbicacionId,
            UbicacionDestinoId = tecnologia.UbicacionId,
            EstadoEnvioId = estado.EstadoEnvioId,
            Direccion = DireccionEnvioEnum.HaciaTecnologia,
            UsuarioSolicitanteId = UsuarioId,
            FechaCreacion = DateTime.UtcNow
        };

        // El caso reportado: privado en tránsito hacia Tecnología.
        var envioPrivado = Nuevo("ENV-PRIVADO", transito);
        var envioInterno = Nuevo("ENV-INSTITUCIONAL", transito);
        // Despachado por Tecnología y todavía sin transporte: espera chofer.
        var envioSinTransporte = Nuevo("ENV-SIN-TRANSPORTE", enTransportacion);
        db.Envios.AddRange(envioPrivado, envioInterno, envioSinTransporte);
        await db.SaveChangesAsync();

        db.Transportes.AddRange(
            new Transporte { EnvioId = envioPrivado.EnvioId, TipoTransporteId = privado.TipoTransporteId },
            new Transporte { EnvioId = envioInterno.EnvioId, TipoTransporteId = interno.TipoTransporteId });
        await db.SaveChangesAsync();
        return db;
    }
}
