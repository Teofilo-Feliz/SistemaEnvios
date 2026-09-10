using Microsoft.AspNetCore.Http;
using SistemaEnvios.Api.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// La política de contenido depende de qué devuelve cada respuesta.
///
/// Mientras esto fue solo un API de JSON, "default-src 'none'" era exacto. Al unificar el
/// contenedor este mismo proceso pasó a servir el SPA, y esa cabecera se aplicó al index.html: el
/// navegador descargaba la página con 200 y se negaba a ejecutar su propio bundle. Pantalla en
/// blanco, ninguna capa fallando, y el motivo escrito solo en la consola del navegador.
/// </summary>
public sealed class CabecerasSeguridadTests
{
    private const string Emisor = "https://authserverqa.rehabilitacion.org.do";

    private static HttpContext Contexto(string ruta, bool https = true)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Path = ruta;
        contexto.Request.Scheme = https ? "https" : "http";
        return contexto;
    }

    private static string Csp(HttpContext c) => c.Response.Headers["Content-Security-Policy"].ToString();

    [Fact]
    public void ElSpaPuedeCargarSuPropioBundle()
    {
        var contexto = Contexto("/");

        CabecerasSeguridad.Aplicar(contexto, CabecerasSeguridad.PoliticaSpa(Emisor));

        Assert.Contains("script-src 'self'", Csp(contexto));
    }

    /// <summary>
    /// 'unsafe-inline' en script-src es el permiso que convierte un XSS en ejecución. El
    /// index.html no lleva scripts en línea, así que no hace falta y no debe aparecer.
    /// </summary>
    [Fact]
    public void ElSpaNoHabilitaScriptsEnLinea()
    {
        var contexto = Contexto("/");

        CabecerasSeguridad.Aplicar(contexto, CabecerasSeguridad.PoliticaSpa(Emisor));

        var directivaScript = Csp(contexto).Split("; ").Single(x => x.StartsWith("script-src"));
        Assert.DoesNotContain("unsafe-inline", directivaScript);
        Assert.DoesNotContain("unsafe-eval", directivaScript);
    }

    /// <summary>oidc-client-ts pide el descubrimiento y canjea el código contra el emisor.</summary>
    [Fact]
    public void ElSpaPuedeHablarConElEmisor()
    {
        var contexto = Contexto("/");

        CabecerasSeguridad.Aplicar(contexto, CabecerasSeguridad.PoliticaSpa(Emisor));

        Assert.Contains($"connect-src 'self' {Emisor}", Csp(contexto));
        // La renovación silenciosa vive en un iframe que navega al emisor y vuelve a este origen.
        Assert.Contains($"frame-src 'self' {Emisor}", Csp(contexto));
    }

    /// <summary>Una respuesta JSON no carga nada, así que se le sigue prohibiendo todo.</summary>
    [Fact]
    public void ApiConservaLaPoliticaEstrictaAunqueElProcesoSirvaElSpa()
    {
        var contexto = Contexto("/api/envios");

        CabecerasSeguridad.Aplicar(contexto, CabecerasSeguridad.PoliticaSpa(Emisor));

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Csp(contexto));
    }

    [Fact]
    public void SinSpaTodoConservaLaPoliticaEstricta()
    {
        var contexto = Contexto("/");

        CabecerasSeguridad.Aplicar(contexto, politicaSpa: null);

        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Csp(contexto));
    }

    /// <summary>
    /// El emisor entra por parámetro: QA y producción usan servidores distintos y fijar uno
    /// romperia el otro sin que nada lo delatara hasta desplegarlo.
    /// </summary>
    [Fact]
    public void LaPoliticaUsaElEmisorConfigurado()
    {
        var produccion = CabecerasSeguridad.PoliticaSpa("https://authserver.rehabilitacion.org.do");

        Assert.Contains("https://authserver.rehabilitacion.org.do", produccion);
        Assert.DoesNotContain("authserverqa", produccion);
    }

    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    public void LasDemasCabecerasSiguenPuestas(string nombre, string valor)
    {
        var contexto = Contexto("/");

        CabecerasSeguridad.Aplicar(contexto, CabecerasSeguridad.PoliticaSpa(Emisor));

        Assert.Equal(valor, contexto.Response.Headers[nombre].ToString());
    }

    [Fact]
    public void HstsSoloViajaSobreHttps()
    {
        var seguro = Contexto("/", https: true);
        var inseguro = Contexto("/", https: false);

        CabecerasSeguridad.Aplicar(seguro, CabecerasSeguridad.PoliticaSpa(Emisor));
        CabecerasSeguridad.Aplicar(inseguro, CabecerasSeguridad.PoliticaSpa(Emisor));

        Assert.True(seguro.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.False(inseguro.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }
}
