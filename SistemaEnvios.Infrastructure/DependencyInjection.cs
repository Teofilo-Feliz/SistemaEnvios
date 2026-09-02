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
using SistemaEnvios.Infrastructure.Services.Dashboard;
using SistemaEnvios.Infrastructure.Services.Notificaciones;
using FluentValidation;
using SistemaEnvios.Application.Validators.Envios;
using SistemaEnvios.Application.Validators.Transportes;
using SistemaEnvios.Application.Validators.Recepciones;
using SistemaEnvios.Application.Validators.Incidencias;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Infrastructure.Security;
using SistemaEnvios.Application.Interfaces.Services;
using SistemaEnvios.Application.Interfaces.Services.Dashboard;
using SistemaEnvios.Application.Interfaces.Services.Seguridad;
using SistemaEnvios.Infrastructure.Services.Seguridad;

namespace SistemaEnvios.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SistemaEnvios");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Debe configurar la cadena de conexión 'ConnectionStrings:SistemaEnvios'.");
        }

        services.AddDbContextPool<SistemaEnviosDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IEnvioEquipoRepository, EnvioEquipoRepository>();
        services.AddScoped<IEnvioService, EnvioService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAlcanceEnvios, AlcanceEnvios>();
        services.AddScoped<IEnvioEquipoService, EnvioEquipoService>();
        services.AddScoped<ITransporteService, TransporteService>();
        services.AddScoped<ICatalogoTransporteService, CatalogoTransporteService>();
        services.AddScoped<IRecepcionService, RecepcionService>();
        services.AddScoped<IIncidenciaService, IncidenciaService>();
        services.AddScoped<IEstadoEnvioService, EstadoEnvioService>();
        services.AddScoped<ITransicionEstadoEnvioService, TransicionEstadoEnvioService>();
        services.AddScoped<IHistorialEstadoEnvioService, HistorialEstadoEnvioService>();
        services.AddScoped<IEquipoService, EquipoService>();
        services.AddScoped<ITipoEquipoService, TipoEquipoService>();
        services.AddScoped<IUbicacionService, UbicacionService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddScoped<IPerfilUsuarioService, PerfilUsuarioService>();
        services.AddValidatorsFromAssemblyContaining<CrearEnvioRequestValidator>();
        return services;
    }
}
