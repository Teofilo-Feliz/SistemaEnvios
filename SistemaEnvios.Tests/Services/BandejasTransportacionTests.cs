using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SistemaEnvios.Application.DTOs.Common;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Domain.Enums;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Infrastructure.Services.Transportes;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// Transportación confirma dos cosas distintas y en momentos distintos: que el chofer recibió
/// el equipo y salió a ruta, y que el envío llegó a su punto. Mezclarlas en una sola bandeja
/// confundía, y además obligaba a filtrar después de paginar: una página de diez podía mostrar
/// dos filas porque el resto se descartaba en el navegador.
/// </summary>
public sealed class BandejasTransportacionTests
{
    private static readonly Guid UsuarioId = Guid.Parse("8f69cd83-4ce7-46d8-a74f-f34ce7fd5ef2");

    [Fact]
    public async Task LaBandejaDelChoferSoloTraeLoEntregadoSinConfirmar()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarPendientesDeCustodiaAsync(new ParametrosPaginaSimple());

        Assert.True(resultado.IsSuccess, resultado.Error);
        var fila = Assert.Single(resultado.Value!.Items);
        Assert.Equal("ENV-ESPERA-CHOFER", fila.NumeroEnvio);
        Assert.Equal("Ramón Castillo", fila.NombreChofer);
    }

    [Fact]
    public async Task LaBandejaDeLlegadasSoloTraeLoQueVaEnRuta()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarPendientesDeLlegadaAsync(new ParametrosPaginaSimple());

        Assert.True(resultado.IsSuccess, resultado.Error);
        var fila = Assert.Single(resultado.Value!.Items);
        Assert.Equal("ENV-EN-RUTA", fila.NumeroEnvio);
    }

    [Fact]
    public async Task NingunaBandejaMuestraLoQueYaSeConfirmoEnEsePaso()
    {
        await using var db = await SembrarAsync();
        var servicio = Servicio(db);

        var custodia = await servicio.ListarPendientesDeCustodiaAsync(new ParametrosPaginaSimple());
        var llegada = await servicio.ListarPendientesDeLlegadaAsync(new ParametrosPaginaSimple());

        // El que ya salió a ruta no sigue esperando al chofer, y el que aún no sale no puede llegar.
        Assert.DoesNotContain(custodia.Value!.Items, x => x.NumeroEnvio == "ENV-EN-RUTA");
        Assert.DoesNotContain(llegada.Value!.Items, x => x.NumeroEnvio == "ENV-ESPERA-CHOFER");
    }

    [Fact]
    public async Task ElTransportePrivadoNoEntraEnLasBandejasDeTransportacion()
    {
        await using var db = await SembrarAsync();

        var custodia = await Servicio(db).ListarPendientesDeCustodiaAsync(new ParametrosPaginaSimple());

        // El privado va directo a Tecnología: Transportación nunca lo custodia.
        Assert.DoesNotContain(custodia.Value!.Items, x => x.NumeroEnvio == "ENV-PRIVADO");
    }

    [Fact]
    public async Task ElTotalCuentaSoloLasFilasDeLaBandeja()
    {
        await using var db = await SembrarAsync();

        var resultado = await Servicio(db).ListarPendientesDeCustodiaAsync(new ParametrosPaginaSimple());

        // Antes se paginaban los envíos y se filtraba después, así que el total mentía.
        Assert.Equal(1, resultado.Value!.TotalItems);
    }

    // ---------- apoyo ----------

    private static TransporteService Servicio(SistemaEnviosDbContext db)
    {
        IUserContext u = new FakeUserContext(UsuarioId, "1,AZUA", position: "Encargado de Transportacion");
        return new TransporteService(db, new UnitOfWork(db),
            new CrearTransporteRequestValidator(), new ActualizarTransporteRequestValidator(),
            u, AlcanceDePrueba.Crear(db, u));
    }

    private static async Task<SistemaEnviosDbContext> SembrarAsync()
    {
        var db = new SistemaEnviosDbContext(new DbContextOptionsBuilder<SistemaEnviosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

        var filial = new Ubicacion { Nombre = "Azua", CodigoCentro = "F01", FilialExternaId = 1, Tipo = TipoUbicacionEnum.Filial, Activo = true };
        var tecnologia = new Ubicacion { Nombre = "Tecnología", CodigoCentro = "TEC", Tipo = TipoUbicacionEnum.Tecnologia, Activo = true };
        var entregado = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EntregadoATransportacion, Nombre = "Entregado", Activo = true };
        var transito = new EstadoEnvio { Codigo = EstadoEnvioCodigos.EnTransito, Nombre = "En tránsito", Activo = true };
        var interno = new TipoTransporte { Codigo = "INT", Nombre = "Interno", Estrategia = EstrategiaTransporteEnum.TransportacionInstitucional, Activo = true };
        var privado = new TipoTransporte { Codigo = "PRIV", Nombre = "Privado", Estrategia = EstrategiaTransporteEnum.EntregaDirectaTecnologia, Activo = true };
        var chofer = new ChoferInterno { NombreCompleto = "Ramón Castillo", NumeroEmpleado = "EMP-1", Activo = true };
        db.AddRange(filial, tecnologia, entregado, transito, interno, privado, chofer);
        db.Add(new PerfilPosicion { Posicion = "Encargado de Transportacion", Perfil = (byte)PerfilAlcance.Transportacion });
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
        var esperaChofer = Nuevo("ENV-ESPERA-CHOFER", entregado);
        var enRuta = Nuevo("ENV-EN-RUTA", transito);
        var privadoEnvio = Nuevo("ENV-PRIVADO", entregado);
        db.Envios.AddRange(esperaChofer, enRuta, privadoEnvio);
        await db.SaveChangesAsync();

        Transporte Con(Envio envio, TipoTransporte tipo) => new()
        {
            EnvioId = envio.EnvioId,
            TipoTransporteId = tipo.TipoTransporteId,
            FechaCreacion = DateTime.UtcNow,
            UsuarioCreacionId = UsuarioId
        };
        var tEspera = Con(esperaChofer, interno);
        tEspera.Interno = new TransporteInterno
        {
            ChoferInternoId = chofer.ChoferInternoId,
            NombreChoferAlMomento = chofer.NombreCompleto,
            NumeroEmpleadoAlMomento = chofer.NumeroEmpleado,
            FechaEntregaTransportacion = DateTime.UtcNow,
            EntregaConfirmada = false
        };
        var tRuta = Con(enRuta, interno);
        tRuta.Interno = new TransporteInterno
        {
            ChoferInternoId = chofer.ChoferInternoId,
            NombreChoferAlMomento = chofer.NombreCompleto,
            NumeroEmpleadoAlMomento = chofer.NumeroEmpleado,
            FechaEntregaTransportacion = DateTime.UtcNow,
            EntregaConfirmada = true
        };
        var tPrivado = Con(privadoEnvio, privado);
        tPrivado.Privado = new TransportePrivado
        {
            NombreResponsable = "Un familiar",
            Parentesco = "Hermano",
            DocumentoResponsable = "00100000001",
            PlacaVehiculo = "A123456"
        };
        db.Transportes.AddRange(tEspera, tRuta, tPrivado);
        await db.SaveChangesAsync();
        return db;
    }
}
