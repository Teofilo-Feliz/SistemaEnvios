using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Common;
using SistemaEnvios.Application.DTOs.Casos;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Services.Casos;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Integracion;

/// <summary>
/// El inventario de Tecnología contra el motor real.
///
/// El proveedor InMemory ejecuta cualquier LINQ en memoria, así que una consulta que SQL Server no
/// sepa traducir pasa verde en las pruebas unitarias y revienta en QA. Esta lleva el condicional
/// sobre la dirección del envío dentro del Where y dentro de la proyección, que es la forma que
/// puede no traducirse.
///
/// Solo lee. El alcance va con un doble en vez del real porque el real siembra filas en
/// PerfilesPorPosicion, y esta prueba no escribe en la base a la que se conecte.
/// </summary>
public sealed class EquiposEnTecnologiaSqlServerTests
{
    private static string? Cadena => Environment.GetEnvironmentVariable("SISTEMAENVIOS_TEST_SQL");

    [SkippableTheory]
    [InlineData(null)]
    [InlineData(1)]
    public async Task LaConsultaDelInventarioSeTraduceASql(int? filialId)
    {
        Skip.If(string.IsNullOrWhiteSpace(Cadena), "Defina SISTEMAENVIOS_TEST_SQL para ejecutar contra SQL Server.");

        await using var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>().UseSqlServer(Cadena).Options);

        var servicio = new CasoEquipoService(
            db,
            new UnitOfWork(db),
            new FakeUserContext(Guid.NewGuid(), "1,AZUA", roles: ["Programador Senior"]),
            new AlcanceDeTecnologia());

        // Con y sin filtro de filial: son dos árboles de consulta distintos, y el segundo mete el
        // condicional de la dirección también en el Where.
        var resultado = await servicio.ListarEquiposEnTecnologiaAsync(
            new ConsultarEquiposEnTecnologiaRequest { FilialId = filialId, PageSize = 5 });

        Assert.True(resultado.IsSuccess, resultado.Error);

        // Lo que se comprueba es que la consulta corra y proyecte; cuántas filas haya depende de
        // la base a la que se apunte. Todas tienen que traer ticket: sin caso abierto no salen.
        Assert.All(resultado.Value!.Items, x => Assert.False(string.IsNullOrWhiteSpace(x.NumeroTicket)));
    }

    /// <summary>Tecnología fija, para no tocar las tablas de autoridad de la base real.</summary>
    private sealed class AlcanceDeTecnologia : IAlcanceEnvios
    {
        public Task<PerfilAlcance> ResolverPerfilAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PerfilAlcance.Tecnologia);

        public Task<IQueryable<Envio>> FiltrarAsync(IQueryable<Envio> query, CancellationToken cancellationToken = default) =>
            Task.FromResult(query);

        public Task<IQueryable<Equipo>> FiltrarEquiposAsync(IQueryable<Equipo> query, CancellationToken cancellationToken = default) =>
            Task.FromResult(query);

        public Task<Result> VerificarAsync(int envioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> VerificarUbicacionAsync(int ubicacionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> VerificarFilialMapeadaAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
