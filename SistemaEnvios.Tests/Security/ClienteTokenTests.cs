using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using SistemaEnvios.Api.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// AuthManager no emite el claim "aud" para este cliente —se comprobó contra la instancia real—,
/// así que la audiencia no sirve para separar el token de esta aplicación del de RRHH.
/// "client_id" sí viaja, y es lo que se valida.
///
/// El riesgo de esta comprobación es dejar a todo el mundo fuera, así que se prueba en las dos
/// direcciones: que el cliente propio entre y que uno ajeno no.
/// </summary>
public sealed class ClienteTokenTests
{
    private static ClaimsPrincipal ConClientId(params string[] valores)
    {
        var identidad = new ClaimsIdentity("prueba");
        foreach (var v in valores) identidad.AddClaim(new Claim(ClienteToken.Claim, v));
        return new ClaimsPrincipal(identidad);
    }

    private static IConfiguration Config(params string[] clientes)
    {
        var valores = clientes
            .Select((c, i) => new KeyValuePair<string, string?>($"{ClienteToken.Seccion}:{i}", c));
        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Fact]
    public void ElClienteDeEstaAplicacionEntra()
    {
        Assert.True(ClienteToken.Autorizado(ConClientId("logi-track-app"), ["logi-track-app"]));
    }

    [Fact]
    public void ElTokenDeOtraAplicacionSeRechaza()
    {
        // Es el caso que motiva todo: mismo emisor, misma llave, otra aplicación.
        Assert.False(ClienteToken.Autorizado(ConClientId("rrhh-app"), ["logi-track-app"]));
    }

    [Fact]
    public void UnTokenSinClientIdSeRechaza()
    {
        // No poder comprobar de quién es un token no es razón para aceptarlo.
        var sinClaims = new ClaimsPrincipal(new ClaimsIdentity("prueba"));
        Assert.False(ClienteToken.Autorizado(sinClaims, ["logi-track-app"]));
    }

    [Fact]
    public void LaComparacionIgnoraMayusculas()
    {
        Assert.True(ClienteToken.Autorizado(ConClientId("Logi-Track-App"), ["logi-track-app"]));
    }

    [Fact]
    public void SinConfigurarNoSeValidaYElApiLoAdvierte()
    {
        // Sin lista configurada la comprobación no se monta: es lo que evita que un despliegue
        // sin la variable deje a todo el mundo fuera. A cambio, el arranque avisa.
        Assert.False(ClienteToken.EstaValidado(Config()));
        Assert.Empty(ClienteToken.Configurados(Config()));
    }

    [Fact]
    public void LaListaConfiguradaSeLeeYSeLimpia()
    {
        var configurados = ClienteToken.Configurados(Config("logi-track-app", "  ", " otra-app "));

        Assert.Equal(["logi-track-app", "otra-app"], configurados);
    }
}
