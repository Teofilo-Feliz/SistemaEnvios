using SistemaEnvios.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SistemaEnvios.Api.Security;
using SistemaEnvios.Application.Interfaces.Security;
using System.Text.Json.Serialization;
using System.Text;

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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DemoAuth:Enabled"))
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["DemoAuth:SigningKey"]!)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["DemoAuth:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["DemoAuth:Audience"],
                RoleClaimType = "roles",
                NameClaimType = "name"
            };
        }
        else
        {
            options.Authority = builder.Configuration["Authentication:Authority"];
            options.Audience = builder.Configuration["Authentication:Audience"];
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, RoleClaimType = "roles", NameClaimType = "name" };
        }
    });
builder.Services.AddApplicationAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});
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

app.Run();
