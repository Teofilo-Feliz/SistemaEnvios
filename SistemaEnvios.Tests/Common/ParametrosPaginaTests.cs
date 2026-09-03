using SistemaEnvios.Application.DTOs.Common;

namespace SistemaEnvios.Tests.Common;

/// <summary>
/// El tope de página es lo que impide que un cliente pida el inventario completo con
/// pageSize=100000. Como la regla se repite en cada listado, vive en un solo lugar.
/// </summary>
public sealed class ParametrosPaginaTests
{
    private sealed class Parametros : ParametrosPagina;

    [Fact]
    public void PorDefectoDevuelveLaPrimeraPaginaConTamanoRazonable()
    {
        var parametros = new Parametros();

        Assert.Equal(1, parametros.PaginaNormalizada);
        Assert.Equal(ParametrosPagina.TamanoPorDefecto, parametros.TamanoNormalizado);
        Assert.Equal(0, parametros.Salto);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void UnaPaginaMenorQueUnoSeTrataComoLaPrimera(int page)
    {
        Assert.Equal(1, new Parametros { Page = page }.PaginaNormalizada);
    }

    [Fact]
    public void UnTamanoDesmedidoSeRecortaAlMaximo()
    {
        var parametros = new Parametros { PageSize = 100_000 };

        Assert.Equal(ParametrosPagina.TamanoMaximo, parametros.TamanoNormalizado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UnTamanoInvalidoCaeAlValorPorDefectoYNoADevolverNada(int pageSize)
    {
        Assert.Equal(ParametrosPagina.TamanoPorDefecto, new Parametros { PageSize = pageSize }.TamanoNormalizado);
    }

    [Fact]
    public void ElSaltoSeCalculaConLosValoresYaNormalizados()
    {
        var parametros = new Parametros { Page = 3, PageSize = 25 };

        Assert.Equal(50, parametros.Salto);
        Assert.Equal(0, new Parametros { Page = -2, PageSize = 10 }.Salto);
    }

    [Fact]
    public void LaPaginaConoceElTotalDePaginasSinQueCadaServicioLoCalcule()
    {
        var pagina = PaginaResponse<string>.Crear(["a", "b"], new Parametros { Page = 2, PageSize = 2 }, totalItems: 7);

        Assert.Equal(2, pagina.Page);
        Assert.Equal(2, pagina.PageSize);
        Assert.Equal(7, pagina.TotalItems);
        Assert.Equal(4, pagina.TotalPages);
    }

    [Fact]
    public void SinResultadosSigueHabiendoCeroPaginas()
    {
        var pagina = PaginaResponse<string>.Crear([], new Parametros(), totalItems: 0);

        Assert.Empty(pagina.Items);
        Assert.Equal(0, pagina.TotalPages);
    }
}
