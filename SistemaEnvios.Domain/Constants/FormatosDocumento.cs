using System.Text.RegularExpressions;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Domain.Constants;

/// <summary>
/// Qué se acepta en el documento del responsable, según su tipo.
/// </summary>
/// <remarks>
/// La regla vive aquí una sola vez porque se aplica en tres capas —el validador de la petición, la
/// normalización del servicio y el CHECK de la tabla— y son tres sitios donde una copia se queda
/// vieja sin que nada avise: el formulario acepta lo que la base rechaza, y el usuario recibe un
/// error de base de datos por escribir algo que la pantalla le dejó escribir.
/// </remarks>
public static partial class FormatosDocumento
{
    /// <summary>Cédula dominicana: once dígitos, sin guiones ni espacios.</summary>
    public const string PatronCedula = "^[0-9]{11}$";

    /// <summary>
    /// Pasaporte: letras y dígitos, de 6 a 15. Sin espacios ni guiones, que es como se teclea el
    /// número impreso en la libreta. Se guarda en mayúsculas para que dos personas que escriben el
    /// mismo pasaporte con distinta caja no generen dos registros distintos.
    /// </summary>
    public const string PatronPasaporte = "^[A-Z0-9]{6,15}$";

    public const int LargoMaximo = 15;

    public static string Patron(TipoDocumentoEnum tipo) =>
        tipo == TipoDocumentoEnum.Pasaporte ? PatronPasaporte : PatronCedula;

    /// <summary>
    /// Deja el documento como se guarda: la cédula sin nada que no sea dígito —así un
    /// "402-0981537-8" tecleado con guiones entra igual—, y el pasaporte en mayúsculas sin
    /// espacios.
    /// </summary>
    public static string Normalizar(TipoDocumentoEnum tipo, string valor) =>
        tipo == TipoDocumentoEnum.Pasaporte
            ? SinEspacios().Replace(valor ?? string.Empty, string.Empty).ToUpperInvariant()
            : NoDigitos().Replace(valor ?? string.Empty, string.Empty);

    public static bool EsValido(TipoDocumentoEnum tipo, string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Regex.IsMatch(Normalizar(tipo, valor), Patron(tipo));

    [GeneratedRegex(@"[^0-9]")]
    private static partial Regex NoDigitos();

    [GeneratedRegex(@"\s")]
    private static partial Regex SinEspacios();
}
