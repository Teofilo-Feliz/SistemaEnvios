namespace SistemaEnvios.Api.Security;

/// <summary>
/// Este API solo responde JSON. Las cabeceras le quitan al navegador el margen de tratarlo como
/// otra cosa: adivinar el tipo de contenido, enmarcarlo en una página ajena, cargar recursos
/// desde una respuesta, o mandar la ruta consultada en el Referer hacia afuera.
/// </summary>
public static class CabecerasSeguridad
{
    private static readonly (string Nombre, string Valor)[] Base =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),
        ("Referrer-Policy", "no-referrer"),
        ("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'"),
        ("X-Permitted-Cross-Domain-Policies", "none")
    ];

    public static void Aplicar(HttpContext contexto)
    {
        foreach (var (nombre, valor) in Base)
            if (!contexto.Response.Headers.ContainsKey(nombre))
                contexto.Response.Headers[nombre] = valor;

        // Solo sobre HTTPS: enviarla por http la ignora el navegador, y en desarrollo dejaría
        // el dominio fijado a HTTPS durante un año en la máquina de quien probó.
        if (contexto.Request.IsHttps && !contexto.Response.Headers.ContainsKey("Strict-Transport-Security"))
            contexto.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }
}
