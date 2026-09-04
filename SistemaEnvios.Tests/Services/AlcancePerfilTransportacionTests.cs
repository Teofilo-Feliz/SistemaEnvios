using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Application.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Tests.Security;

namespace SistemaEnvios.Tests.Services;

/// <summary>
/// La filial necesita transportes.gestionar para registrar el transporte de su propio envío.
/// Como el perfil sale de la posición y no de los permisos, tenerlo no la convierte en
/// Transportación ni le quita la vista de sus envíos.
/// </summary>
public sealed class AlcancePerfilTransportacionTests
{
    private static readonly Guid UsuarioId = Guid.Parse("f0a1c2d3-4e5f-4a6b-8c9d-0e1f2a3b4c5d");
    private const string Sede = "30,SANTO DOMINGO (SEDE)";

    [Fact]
    public async Task FilialConPermisoDeGestionarTransporte_SigueSiendoPerfilFilial()
    {
        await using var db = await CrearContextoAsync();
        var usuario = new FakeUserContext(
            UsuarioId, Sede,
            permissions: [PermissionNames.TransportesGestionar],
            position: "Asistente Administrativo");

        Assert.Equal(PerfilAlcance.Filial, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    [Fact]
    public async Task PosicionDeTransportacion_ConLosMismosPermisos_EsPerfilTransportacion()
    {
        await using var db = await CrearContextoAsync();
        var usuario = new FakeUserContext(
            UsuarioId, Sede,
            permissions: [PermissionNames.TransportesGestionar],
            position: "Encargado de Transportacion");

        Assert.Equal(PerfilAlcance.Transportacion, await AlcanceDePrueba.Crear(db, usuario).ResolverPerfilAsync());
    }

    private static async Task<SistemaEnviosDbContext> CrearContextoAsync()
    {
        var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.PerfilesPorPosicion.AddRange(
            new PerfilPosicion { Posicion = "Asistente Administrativo", Perfil = (byte)PerfilAlcance.Filial },
            new PerfilPosicion { Posicion = "Encargado de Transportacion", Perfil = (byte)PerfilAlcance.Transportacion });
        await db.SaveChangesAsync();
        return db;
    }
}
