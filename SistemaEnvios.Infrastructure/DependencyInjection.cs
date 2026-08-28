using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Repositories;
using SistemaEnvios.Infrastructure.Repositories.Envios;
using SistemaEnvios.Infrastructure.Services.Envios;
using SistemaEnvios.Infrastructure.Services.Transportes;
using SistemaEnvios.Infrastructure.Services.Recepciones;
using SistemaEnvios.Infrastructure.Services.Incidencias;
using SistemaEnvios.Infrastructure.Services.Estados;
using SistemaEnvios.Infrastructure.Services.Equipos;
using SistemaEnvios.Infrastructure.Services.TiposEquipos;
using SistemaEnvios.Infrastructure.Services.Ubicaciones;
using FluentValidation;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Application.Validators.Incidencias;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Services;

namespace SistemaEnvios.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SistemaEnviosDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("SistemaEnvios")));
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IEnvioEquipoRepository, EnvioEquipoRepository>();
        services.AddScoped<IEnvioService, EnvioService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEnvioEquipoService, EnvioEquipoService>();
        services.AddScoped<ITransporteService, TransporteService>();
        services.AddScoped<IRecepcionService, RecepcionService>();
        services.AddScoped<IIncidenciaService, IncidenciaService>();
        services.AddScoped<IEstadoEnvioService, EstadoEnvioService>();
        services.AddScoped<ITransicionEstadoEnvioService, TransicionEstadoEnvioService>();
        services.AddScoped<IHistorialEstadoEnvioService, HistorialEstadoEnvioService>();
        services.AddScoped<IEquipoService, EquipoService>();
        services.AddScoped<ITipoEquipoService, TipoEquipoService>();
        services.AddScoped<IUbicacionService, UbicacionService>();
        services.AddValidatorsFromAssemblyContaining<CrearEnvioRequestValidator>();
        return services;
    }
}
