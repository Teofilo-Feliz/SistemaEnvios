using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Domain.Constants;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// La máquina de estados vive en datos, no en código, así que ninguna prueba unitaria la
/// protege: al recrear la base desde el script se puede perder un cambio de flujo y todo sigue
/// compilando y en verde hasta que alguien pulsa un botón. Ya pasó dos veces.
///
/// Esto fija el contrato: las transiciones que el código necesita tienen que estar activas, y
/// las que se retiraron no pueden volver.
/// </summary>
public sealed class MaquinaEstadosSqlServerTests
{
    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    /// <summary>Interno filial → Tecnología: entregar y salir a ruta es un solo acto.</summary>
    public static TheoryData<string, string> Requeridas
    {
        get
        {
            var datos = new TheoryData<string, string>();
            datos.Add(EstadoEnvioCodigos.EnFilial, EstadoEnvioCodigos.EntregadoATransportacion);
            datos.Add(EstadoEnvioCodigos.EntregadoATransportacion, EstadoEnvioCodigos.EnTransito);
            datos.Add(EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.RecibidoPorTransportacion);
            datos.Add(EstadoEnvioCodigos.RecibidoPorTransportacion, EstadoEnvioCodigos.RecibidoPorTecnologia);
            // Privado: no pasa por Transportación y conserva espera y revisión.
            datos.Add(EstadoEnvioCodigos.EnFilial, EstadoEnvioCodigos.DespachadoTransportePrivado);
            datos.Add(EstadoEnvioCodigos.DespachadoTransportePrivado, EstadoEnvioCodigos.EnTransito);
            datos.Add(EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.EnEsperaDeTecnologia);
            datos.Add(EstadoEnvioCodigos.EnEsperaDeTecnologia, EstadoEnvioCodigos.EnProcesoDeRevision);
            datos.Add(EstadoEnvioCodigos.EnProcesoDeRevision, EstadoEnvioCodigos.RecibidoPorTecnologia);
            // Tecnología → filial: asignar chofer manda a ruta; la filial recibe desde tránsito.
            datos.Add(EstadoEnvioCodigos.EnPreparacionTecnologia, EstadoEnvioCodigos.DespachadoPorTecnologia);
            datos.Add(EstadoEnvioCodigos.DespachadoPorTecnologia, EstadoEnvioCodigos.EnTransportacion);
            datos.Add(EstadoEnvioCodigos.EnTransportacion, EstadoEnvioCodigos.EnTransito);
            datos.Add(EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.RecibidoEnFilial);
            return datos;
        }
    }

    /// <summary>Pasos que se retiraron por no representar ninguna decisión real.</summary>
    public static TheoryData<string, string> Retiradas
    {
        get
        {
            var datos = new TheoryData<string, string>();
            datos.Add(EstadoEnvioCodigos.EntregadoATransportacion, EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion);
            datos.Add(EstadoEnvioCodigos.EnProcesoConfirmacionTransportacion, EstadoEnvioCodigos.ConfirmadoPorTransportacion);
            datos.Add(EstadoEnvioCodigos.ConfirmadoPorTransportacion, EstadoEnvioCodigos.EnTransito);
            datos.Add(EstadoEnvioCodigos.RecibidoPorTransportacion, EstadoEnvioCodigos.EnEsperaDeTecnologia);
            datos.Add(EstadoEnvioCodigos.EnTransportacion, EstadoEnvioCodigos.TransporteAsignado);
            datos.Add(EstadoEnvioCodigos.TransporteAsignado, EstadoEnvioCodigos.DespachadoPorTransportacion);
            datos.Add(EstadoEnvioCodigos.DespachadoPorTransportacion, EstadoEnvioCodigos.EnTransito);
            datos.Add(EstadoEnvioCodigos.EnTransito, EstadoEnvioCodigos.PendienteRecepcionFilial);
            datos.Add(EstadoEnvioCodigos.PendienteRecepcionFilial, EstadoEnvioCodigos.RecibidoEnFilial);
            datos.Add(EstadoEnvioCodigos.RecibidoEnFilial, EstadoEnvioCodigos.RecepcionValidadaEnFilial);
            return datos;
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Requeridas))]
    public async Task LaTransicionQueElFlujoNecesitaEstaActiva(string origen, string destino)
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        Assert.True(await ExisteAsync(origen, destino), $"Falta la transición {origen} → {destino}.");
    }

    [SkippableTheory]
    [MemberData(nameof(Retiradas))]
    public async Task LaTransicionRetiradaNoVuelveAlFlujo(string origen, string destino)
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        Assert.False(await ExisteAsync(origen, destino), $"La transición {origen} → {destino} volvió al flujo.");
    }

    [SkippableFact]
    public async Task RecibidoEnFilialCierraElFlujoHaciaLaFilial()
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");
        await using var db = Contexto();

        var estado = await db.EstadosEnvio.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Codigo == EstadoEnvioCodigos.RecibidoEnFilial);

        Assert.NotNull(estado);
        Assert.True(estado!.Activo, "RECIBIDO_FILIAL debe estar activo.");
        Assert.True(estado.EsFinal, "RECIBIDO_FILIAL debe ser final: es el cierre del flujo hacia filial.");
    }

    private static async Task<bool> ExisteAsync(string origen, string destino)
    {
        await using var db = Contexto();
        return await db.TransicionesEstadoEnvio.AsNoTracking()
            .AnyAsync(t => t.Activo
                           && db.EstadosEnvio.Any(o => o.EstadoEnvioId == t.EstadoOrigenId && o.Codigo == origen)
                           && db.EstadosEnvio.Any(d => d.EstadoEnvioId == t.EstadoDestinoId && d.Codigo == destino));
    }

    private static SistemaEnviosDbContext Contexto() => new(
        new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);
}
