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
        options.UseAspNetCore();
        options.UseSystemNetHttp();
    });
builder.Services.AddApplicationAuthorization();
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();

app.Run();
