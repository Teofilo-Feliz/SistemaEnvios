namespace SistemaEnvios.Domain.Enums;

/// <summary>
/// Documento con el que se identifica al responsable de un transporte privado.
/// </summary>
/// <remarks>
/// No es un detalle cosmético: decide qué se acepta en el campo. La cédula dominicana son once
/// dígitos y nada más, así que ahí una letra es siempre un error de tecleo. El pasaporte casi
/// nunca es solo numérico —el dominicano empieza por letras— y exigirle dígitos dejaría fuera a
/// cualquier responsable extranjero.
/// </remarks>
public enum TipoDocumentoEnum : byte
{
    Cedula = 1,
    Pasaporte = 2
}
