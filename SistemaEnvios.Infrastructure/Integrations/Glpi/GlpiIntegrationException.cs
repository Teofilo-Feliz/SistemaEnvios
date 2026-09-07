namespace SistemaEnvios.Infrastructure.Integrations.Glpi;

/// <summary>
/// Falla propia de la integración con GLPI (sesión rechazada, respuesta ilegible). Existe para
/// distinguirla de un error de programación: la captura <see cref="GlpiClient"/> y la traduce a
/// un Result fallido, nunca escapa al pipeline del API.
/// </summary>
public sealed class GlpiIntegrationException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);
