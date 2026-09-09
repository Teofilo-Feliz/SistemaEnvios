using System.Threading.RateLimiting;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using OpenIddict.Validation.AspNetCore;
using SistemaEnvios.Api.Infrastructure;
using SistemaEnvios.Api.Security;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Infrastructure;
using SistemaEnvios.Infrastructure.Persistence;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole();
}

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sql-server");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IClaimsTransformation, PermisosPorPosicionTransformation>();
// El emisor no vive en appsettings.json sino en el archivo de cada entorno, para que producción
// no herede el de QA por descuido: un default compartido aquí significaría aceptar tokens del
// servidor equivocado sin que nada lo delate. A cambio, su ausencia tiene que ser explícita.
var emisor = builder.Configuration["Authentication:Authority"];
if (string.IsNullOrWhiteSpace(emisor))
{
    throw new InvalidOperationException(
        "Debe configurar 'Authentication:Authority' con el emisor de AuthManager. " +
        $"El entorno actual es '{builder.Environment.EnvironmentName}': compruebe que exista " +
        $"appsettings.{builder.Environment.EnvironmentName}.json —en Linux el nombre distingue " +
        "mayúsculas— o defina la variable de entorno Authentication__Authority.");
}

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(emisor);
        // Todas las apps de la institución comparten emisor: sin audiencia, el token de RRHH
        // también abre este API. El valor lo define AuthManager al registrar el cliente.
        var audiencias = AudienciaToken.Configuradas(builder.Configuration);
        if (audiencias.Length > 0) options.AddAudiences(audiencias);
        options.UseAspNetCore();
        options.UseSystemNetHttp();
    });
builder.Services.AddApplicationAuthorization();
// Ventana fija por usuario: contiene el bucle accidental del front y el barrido de un script,
// sin estorbar el uso normal (una pantalla del sistema no pasa de una docena de llamadas).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            LimitadorPeticiones.Particion(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = LimitadorPeticiones.PeticionesPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
// La imagen de despliegue trae el frontend compilado en wwwroot y lo sirve esta misma
// aplicación. Se decide por la presencia del archivo y no por una bandera de configuración
// porque es el hecho que importa: si el index.html está ahí, el navegador pide todo al mismo
// origen. Sin él seguimos siendo solo API y el frontend vive en otra parte.
var rutaWebRoot = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
var sirveSpa = File.Exists(Path.Combine(rutaWebRoot, "index.html"));

// Sin orígenes la política se registra vacía y el navegador rechaza cada llamada, pero el
// arranque parece exitoso: el fallo aparece recién el día del despliegue. Mejor no arrancar.
// Salvo que sirvamos el SPA: entonces no hay origen cruzado que permitir y exigirlo obligaría
// a inventar un valor para satisfacer una comprobación que ya no protege de nada.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length == 0 && !sirveSpa)
{
    throw new InvalidOperationException(
        "Debe configurar 'Cors:AllowedOrigins' con los orígenes del frontend. " +
        "Sin ellos la API responde pero el navegador bloquea todas las peticiones.");
}
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
        options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
}

// Detrás del proxy del clúster el contenedor recibe HTTP plano en 8080. Sin leer las cabeceras
// reenviadas, UseHttpsRedirection ve "http" y responde una redirección a https; el proxy la
// reenvía otra vez como http y el navegador queda dando vueltas. Las redes y proxies conocidos
// se limpian porque en Swarm la IP del proxy la asigna Docker en cada despliegue.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddInfrastructure(builder.Configuration);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Lo primero del todo: el esquema y la IP reales condicionan lo que hace el resto del
// pipeline, empezando por la redirección a HTTPS de más abajo.
app.UseForwardedHeaders();
app.UseExceptionHandler();
// Antes que nada, para que también viajen en las respuestas de error y en las de health.
app.Use(async (context, next) => { CabecerasSeguridad.Aplicar(context); await next(); });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Antes de autenticar y del limitador: los archivos del SPA son públicos por definición —el
// navegador tiene que poder descargarlos para llegar a la pantalla de inicio de sesión— y una
// sola carga de página pide decenas de ellos, que agotarían la ventana del limitador.
if (sirveSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

if (corsOrigins.Length > 0)
    app.UseCors("Frontend");
app.UseAuthentication();

// AuthManager firma con la misma llave los tokens de toda la institución, así que validar solo
// el emisor deja entrar el token de RRHH igual que el propio. Se comprobó contra la instancia
// real: NO emite el claim "aud" para este cliente, de modo que 'Authentication:Audiences' no
// sirve —configurarla rechazaría todos los tokens—. Lo que sí viaja es "client_id", que
// identifica la aplicación para la que se emitió el token. Es por ahí por donde se cierra.
var clientesAdmitidos = ClienteToken.Configurados(app.Configuration);
if (clientesAdmitidos.Length > 0)
{
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            !ClienteToken.Autorizado(context.User, clientesAdmitidos))
        {
            // Se registra el client_id visto —no es un secreto, identifica a la aplicación y no
            // a la persona— porque si esta lista se configura mal, el síntoma es "nadie entra"
            // y sin esta línea no habría forma de saber qué valor poner.
            var vistos = ClienteToken.DelPrincipal(context.User);
            app.Logger.LogWarning(
                "Token rechazado: fue emitido para {Cliente}, que no está en '{Seccion}'.",
                vistos.Count == 0 ? "(sin client_id)" : string.Join(", ", vistos), ClienteToken.Seccion);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next();
    });
}
else
{
    app.Logger.LogWarning(
        "El API acepta cualquier token emitido por {Emisor}, incluidos los de otras aplicaciones de la institución. Configure '{Seccion}' con el client_id de esta aplicación.",
        app.Configuration["Authentication:Authority"], ClienteToken.Seccion);
}

// Descubrimiento de la audiencia. Se conserva por si algún día AuthManager empieza a emitirla:
// entonces se podría validar también por ahí, que es lo estándar.
if (!AudienciaToken.EstaValidada(app.Configuration))
{
    var audienciasVistas = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            foreach (var audiencia in AudienciaToken.DelPrincipal(context.User))
                if (audienciasVistas.TryAdd(audiencia, 0))
                    app.Logger.LogInformation(
                        "El token ahora trae audiencia '{Audiencia}'. Puede validarse también por '{Seccion}'.",
                        audiencia, AudienciaToken.Seccion);
        }
        await next();
    });
}

// Entre autenticar y autorizar: ya se conoce el 'sub' para particionar, y las peticiones
// que autorización rechazará todavía cuentan; son justo las del abuso sin credenciales.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false }).AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready").AllowAnonymous().DisableRateLimiting();

if (sirveSpa)
{
    // Una ruta /api que no existe tiene que seguir siendo 404: si cayera en el fallback de
    // abajo, el frontend recibiría el index.html con un 200 y reventaría al leerlo como JSON,
    // que es un síntoma mucho más difícil de rastrear que un 404.
    app.MapFallback("/api/{**resto}", () => Results.NotFound())
        .AllowAnonymous().DisableRateLimiting();
    // El enrutado del SPA vive en el navegador: al recargar /envios/123 el servidor no conoce
    // esa ruta y debe devolver el index.html para que Vue Router resuelva. Anónimo porque la
    // política por defecto exige usuario autenticado y aquí todavía no hay token; sin esto un
    // enlace directo respondería 401 en lugar de la pantalla de inicio de sesión.
    app.MapFallbackToFile("index.html").AllowAnonymous().DisableRateLimiting();
}

app.Run();
