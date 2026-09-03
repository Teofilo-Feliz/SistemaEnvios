using System.Threading.RateLimiting;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication;
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
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["Authentication:Authority"]!);
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
// Sin orígenes la política se registra vacía y el navegador rechaza cada llamada, pero el
// arranque parece exitoso: el fallo aparece recién el día del despliegue. Mejor no arrancar.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Debe configurar 'Cors:AllowedOrigins' con los orígenes del frontend. " +
        "Sin ellos la API responde pero el navegador bloquea todas las peticiones.");
}
builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddInfrastructure(builder.Configuration);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

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
app.UseCors("Frontend");
app.UseAuthentication();
// Sin audiencia configurada el API queda abierto a tokens de otras aplicaciones. No se puede
// adivinar el valor, así que se registra el que traen los tokens que sí entran: con eso se
// llena 'Authentication:Audiences' y el aviso desaparece.
if (!AudienciaToken.EstaValidada(app.Configuration))
{
    app.Logger.LogWarning(
        "La audiencia del token no está validada: este API acepta cualquier token emitido por {Emisor}, incluidos los de otras aplicaciones. Configure '{Seccion}'.",
        app.Configuration["Authentication:Authority"], AudienciaToken.Seccion);
    var audienciasVistas = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    app.Use(async (context, next) =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
            foreach (var audiencia in AudienciaToken.DelPrincipal(context.User))
                if (audienciasVistas.TryAdd(audiencia, 0))
                    app.Logger.LogWarning(
                        "Token aceptado con audiencia '{Audiencia}'. Agréguela a '{Seccion}' para que el API deje de aceptar tokens de otras aplicaciones.",
                        audiencia, AudienciaToken.Seccion);
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

app.Run();
