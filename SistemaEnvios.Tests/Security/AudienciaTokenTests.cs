using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using SistemaEnvios.Api.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// AuthManager emite tokens para varias aplicaciones con el mismo emisor. Validar solo el
/// emisor deja entrar el token de RRHH (aud https://humareh.rehabilitacion.org.do) en este API.
/// Como la audiencia real de LogiTrack la define AuthManager y no el código, se configura; y
/// mientras no esté configurada el arranque tiene que decirlo en vez de callar.
/// </summary>
public sealed class AudienciaTokenTests
{
    [Fact]
    public void SinConfiguracion_NoHayAudienciasYQuedaSinValidar()
    {
        var configuradas = AudienciaToken.Configuradas(Config([]));

        Assert.Empty(configuradas);
        Assert.False(AudienciaToken.EstaValidada(Config([])));
    }

    [Fact]
    public void ConConfiguracion_SeValidaYSeDevuelvenLasAudiencias()
    {
        var config = Config([("Authentication:Audiences:0", "logi-track-api")]);

        Assert.True(AudienciaToken.EstaValidada(config));
        Assert.Equal(["logi-track-api"], AudienciaToken.Configuradas(config));
    }

    [Fact]
    public void ValoresVaciosNoCuentanComoAudienciaConfigurada()
    {
        var config = Config([("Authentication:Audiences:0", "   ")]);

        Assert.False(AudienciaToken.EstaValidada(config));
    }

    [Fact]
    public void SeLeenLasAudienciasDelPrincipalParaPoderDiagnosticarlas()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("aud", "https://humareh.rehabilitacion.org.do"),
            new Claim("aud", "logi-track-api"),
            new Claim("sub", "x")
        ]));

        var audiencias = AudienciaToken.DelPrincipal(principal);

        Assert.Equal(["https://humareh.rehabilitacion.org.do", "logi-track-api"], audiencias);
    }

    private static IConfiguration Config((string, string)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(x => new KeyValuePair<string, string?>(x.Item1, x.Item2)))
            .Build();
}
