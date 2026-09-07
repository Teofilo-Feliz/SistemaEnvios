namespace SistemaEnvios.Application.Common;

/// <summary>
/// Reglas de formato del número de ticket, en un solo lugar.
/// </summary>
/// <remarks>
/// Estaban repetidas en los dos validadores, en la creación con equipos y en la consulta de
/// disponibilidad, y habían quedado distintas entre sí: el mismo "0" se rechazaba al agregar un
/// equipo y se aceptaba al editarlo o al preguntar si estaba libre.
/// </remarks>
public static class NumeroTicket
{
    public const int LargoMinimo = 3;
    public const int LargoMaximo = 50;

    public const string MensajeFormato =
        "El número de ticket debe tener entre 3 y 50 dígitos, sin letras ni símbolos.";

    public const string MensajeMayorQueCero =
        "El número de ticket debe ser mayor que cero.";

    /// <summary>Dígitos, del largo permitido y distinto de cero.</summary>
    public static bool EsValido(string? valor) =>
        TieneFormato(valor) && EsMayorQueCero(valor);

    public static bool TieneFormato(string? valor)
    {
        var ticket = valor?.Trim();
        return !string.IsNullOrEmpty(ticket)
            && ticket.Length >= LargoMinimo
            && ticket.Length <= LargoMaximo
            && ticket.All(char.IsAsciiDigit);
    }

    /// <summary>
    /// En la mesa de ayuda no existe el caso cero, así que "0" o "000" no identifican nada.
    /// Se mira dígito a dígito y no con int.Parse: el campo admite hasta 50 dígitos y un número
    /// de ese largo desborda cualquier entero, con lo que la comprobación se caería sola.
    /// Los ceros a la izquierda sí se admiten: "007" es el ticket 7.
    /// </summary>
    public static bool EsMayorQueCero(string? valor)
    {
        var ticket = valor?.Trim();
        return !string.IsNullOrEmpty(ticket) && ticket.Any(c => c is > '0' and <= '9');
    }
}
