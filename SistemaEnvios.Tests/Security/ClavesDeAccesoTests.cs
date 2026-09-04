using System.Security.Claims;
using SistemaEnvios.Api.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// Cuando entra un usuario cuya posición y roles no están mapeados, queda autenticado pero sin
/// permisos, y las pantallas responden 403 sin decir por qué. El sistema deja constancia de las
/// claves exactas que trae el token para poder mapearlas, en vez de tener que adivinarlas.
/// </summary>
public sealed class ClavesDeAccesoTests
{
    [Fact]
    public void DescribeLaPosicionYLosRolesQueTraeElToken()
    {
        var principal = Principal(("position", "Administrador de Filial"), ("roles", "LogiTrack.Filial,evaluador"));

        var claves = ClavesDeAcceso.Describir(principal);

        Assert.Equal("posición 'Administrador de Filial'; roles 'LogiTrack.Filial', 'evaluador'", claves);
    }

    [Fact]
    public void UnTokenSinRolesLoDiceEnVezDeDejarloEnBlanco()
    {
        var principal = Principal(("position", "Asistente"));

        Assert.Equal("posición 'Asistente'; sin roles", ClavesDeAcceso.Describir(principal));
    }

    [Fact]
    public void UnTokenSinPosicionTambienSeDescribe()
    {
        var principal = Principal(("roles", "LogiTrack.Filial"));

        Assert.Equal("sin posición; roles 'LogiTrack.Filial'", ClavesDeAcceso.Describir(principal));
    }

    [Fact]
    public void LosRolesRepetidosNoSeListanDosVeces()
    {
        var principal = Principal(("roles", "LogiTrack.Filial"), ("roles", "logitrack.filial"));

        Assert.Equal("sin posición; roles 'LogiTrack.Filial'", ClavesDeAcceso.Describir(principal));
    }

    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims) =>
        new(new ClaimsIdentity(claims.Select(x => new Claim(x.Tipo, x.Valor)), "prueba"));
}
