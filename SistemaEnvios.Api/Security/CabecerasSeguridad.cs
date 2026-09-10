namespace SistemaEnvios.Api.Security;

/// <summary>
/// Cabeceras que le quitan al navegador el margen de tratar la respuesta como otra cosa: adivinar
/// el tipo de contenido, enmarcarla en una página ajena, cargar recursos desde ella, o mandar la
/// ruta consultada en el Referer hacia afuera.
/// </summary>
/// <remarks>
/// La política de contenido depende de QUÉ devuelve cada respuesta, no del proceso.
///
/// Mientras esto fue solo un API de JSON, "default-src 'none'" era exacto: una respuesta JSON no
/// carga nada, así que prohibirlo todo no rompe nada y cierra la puerta del todo. Al unificar el
/// contenedor, este mismo proceso pasó a servir también el SPA desde wwwroot, y esa cabecera se
/// aplicó al index.html: el navegador descargaba la página y se negaba a ejecutar su propio
/// bundle. El resultado era una pantalla en blanco, sin fallo en ninguna capa —el HTML llegaba
/// con 200, los assets con 200— y con el motivo escrito solo en la consola del navegador.
///
/// En desarrollo no se veía porque ahí el SPA lo sirve Vite en otro puerto y esta cabecera solo
/// alcanzaba a las respuestas de /api.
///
/// Ahora hay dos políticas. /api conserva la estricta, porque sigue devolviendo solo JSON; el
/// resto recibe la del SPA, que abre lo justo para que la aplicación funcione y nada más.
/// </remarks>
public static class CabecerasSeguridad
{
    /// <summary>Para respuestas de JSON: no cargan nada, así que no se les permite nada.</summary>
    private const string CspApi = "default-src 'none'; frame-ancestors 'none'";

    private static readonly (string Nombre, string Valor)[] Base =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),
        ("Referrer-Policy", "no-referrer"),
        ("X-Permitted-Cross-Domain-Policies", "none")
    ];

    /// <summary>
    /// Política del SPA. Parte de "none" y abre solo lo que la aplicación usa de verdad.
    /// </summary>
    /// <remarks>
    /// Cada permiso responde a algo concreto:
    ///
    ///   script-src 'self'        el bundle propio. El index.html no lleva scripts en línea, así
    ///                            que NO hace falta 'unsafe-inline', que es lo que de verdad
    ///                            importa: es el permiso que convierte un XSS en ejecución.
    ///   style-src  'unsafe-inline' + fonts.googleapis.com
    ///                            SweetAlert y ApexCharts inyectan &lt;style&gt; al vuelo, y main.css
    ///                            importa la tipografía Inter desde Google.
    ///   img-src    data:         los gráficos exportan imágenes como data URI.
    ///   connect-src emisor       oidc-client-ts pide el documento de descubrimiento y canjea el
    ///                            código contra AuthManager por fetch.
    ///   frame-src  emisor        la renovación silenciosa usa un iframe oculto que navega al
    ///                            emisor y vuelve a /silent-renew de este mismo origen.
    ///
    /// El emisor entra por parámetro y no fijo: QA y producción usan servidores distintos, y
    /// escribir uno de los dos aquí romperia el otro sin que nada lo delatara hasta desplegarlo.
    /// </remarks>
    public static string PoliticaSpa(string emisor) =>
        string.Join("; ",
            "default-src 'none'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' data: https://fonts.gstatic.com",
            "img-src 'self' data:",
            $"connect-src 'self' {emisor}",
            $"frame-src 'self' {emisor}",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");

    /// <param name="politicaSpa">
    /// Política para las respuestas que no son de /api. Nula cuando este proceso no sirve el SPA,
    /// y entonces todo mantiene la política estricta del API.
    /// </param>
    public static void Aplicar(HttpContext contexto, string? politicaSpa = null)
    {
        foreach (var (nombre, valor) in Base)
            if (!contexto.Response.Headers.ContainsKey(nombre))
                contexto.Response.Headers[nombre] = valor;

        // /api sigue devolviendo solo JSON, así que conserva la política cerrada aunque este
        // proceso sirva además el SPA.
        var esApi = contexto.Request.Path.StartsWithSegments("/api");
        if (!contexto.Response.Headers.ContainsKey("Content-Security-Policy"))
            contexto.Response.Headers["Content-Security-Policy"] =
                politicaSpa is null || esApi ? CspApi : politicaSpa;

        // Solo sobre HTTPS: enviarla por http la ignora el navegador, y en desarrollo dejaría
        // el dominio fijado a HTTPS durante un año en la máquina de quien probó.
        if (contexto.Request.IsHttps && !contexto.Response.Headers.ContainsKey("Strict-Transport-Security"))
            contexto.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }
}
