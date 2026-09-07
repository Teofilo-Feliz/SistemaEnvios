using SistemaEnvios.Application.Interfaces.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// Soporte técnico entra por su propio módulo, pero sobre los datos manda igual que Global:
/// Tecnología está en un extremo de cada envío y es la única que descarta equipos. La regla
/// vive en EsTecnologia() porque antes eran ocho comparaciones sueltas contra Global, y ocho
/// sitios son ocho oportunidades de olvidar uno en el código que decide quién ve qué.
/// </summary>
public sealed class PerfilTecnologiaTests
{
    [Theory]
    [InlineData(PerfilAlcance.Global)]
    [InlineData(PerfilAlcance.Tecnologia)]
    public void GlobalYTecnologiaMandanSobreTodoElSistema(PerfilAlcance perfil)
    {
        Assert.True(perfil.EsTecnologia());
    }

    [Theory]
    [InlineData(PerfilAlcance.Filial)]
    [InlineData(PerfilAlcance.Transportacion)]
    [InlineData(PerfilAlcance.SinAlcance)]
    public void LosDemasPerfilesNoSonTecnologia(PerfilAlcance perfil)
    {
        Assert.False(perfil.EsTecnologia());
    }

    [Fact]
    public void TecnologiaEsElValor4PorqueLaBaseLoGuardaComoNumero()
    {
        // PerfilesPorPosicion.Perfil es un TINYINT con CHECK (1,2,3,4). Si alguien reordena el
        // enum, las filas ya sembradas pasarían a significar otro perfil sin que nada falle.
        Assert.Equal(0, (int)PerfilAlcance.SinAlcance);
        Assert.Equal(1, (int)PerfilAlcance.Global);
        Assert.Equal(2, (int)PerfilAlcance.Transportacion);
        Assert.Equal(3, (int)PerfilAlcance.Filial);
        Assert.Equal(4, (int)PerfilAlcance.Tecnologia);
    }
}
