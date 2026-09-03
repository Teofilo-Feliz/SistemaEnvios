using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SistemaEnvios.Api.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// El API solo devuelve JSON: no hay razón para que un navegador lo interprete como otra cosa,
/// lo enmarque en un iframe ajeno, o filtre la ruta consultada por Referer.
/// </summary>
public sealed class CabecerasSeguridadTests
{
    [Fact]
    public void AplicaLasCabecerasBaseEnTodaRespuesta()
    {
        var contexto = new DefaultHttpContext();

        CabecerasSeguridad.Aplicar(contexto);

        Assert.Equal("nosniff", contexto.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", contexto.Response.Headers["X-Frame-Options"]);
        Assert.Equal("no-referrer", contexto.Response.Headers["Referrer-Policy"]);
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", contexto.Response.Headers["Content-Security-Policy"]);
    }

    [Fact]
    public void SoloExigeHstsCuandoLaPeticionYaViajaPorHttps()
    {
        var plano = new DefaultHttpContext();
        plano.Request.Scheme = "http";
        CabecerasSeguridad.Aplicar(plano);
        Assert.False(plano.Response.Headers.ContainsKey("Strict-Transport-Security"));

        var seguro = new DefaultHttpContext();
        seguro.Request.Scheme = "https";
        CabecerasSeguridad.Aplicar(seguro);
        Assert.Contains("max-age=", seguro.Response.Headers["Strict-Transport-Security"].ToString());
    }

    [Fact]
    public void NoDuplicaUnaCabeceraYaPresente()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        CabecerasSeguridad.Aplicar(contexto);

        Assert.Equal("SAMEORIGIN", contexto.Response.Headers["X-Frame-Options"]);
    }
}

/// <summary>
/// El límite se reparte por usuario y no por proceso: si se contara global, un solo usuario
/// activo dejaría sin cupo a las otras 33 filiales.
/// </summary>
public sealed class LimitadorPeticionesTests
{
    [Fact]
    public void UsuarioAutenticado_SeParticionaPorSuIdentificador()
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "usuario-7")], "test"))
        };

        Assert.Equal("sub:usuario-7", LimitadorPeticiones.Particion(contexto));
    }

    [Fact]
    public void UsuarioAnonimo_SeParticionaPorDireccionIp()
    {
        var contexto = new DefaultHttpContext();
        contexto.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.9");

        Assert.Equal("ip:10.0.0.9", LimitadorPeticiones.Particion(contexto));
    }

    [Fact]
    public void SinIdentidadNiIp_CaeAUnaParticionUnicaYNoRevienta()
    {
        Assert.Equal("ip:desconocida", LimitadorPeticiones.Particion(new DefaultHttpContext()));
    }
}
