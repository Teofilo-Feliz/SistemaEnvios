using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaEnvios.Application.Interfaces.Security;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;
using SistemaEnvios.Infrastructure.Security;

namespace SistemaEnvios.Tests.Security;

/// <summary>
/// Siembra los perfiles estándar antes de devolver el alcance real.
///
/// Hace falta porque el alcance Global dejó de concederse por el nombre del rol que trajera el
/// token: ahora hay que mapear la posición o el rol, igual que en producción. Los actores de
/// las pruebas tienen que cumplir esa misma regla, o estarían probando un sistema más
/// permisivo que el que se despliega.
/// </summary>
internal static class AlcanceDePrueba
{
    private static readonly (string Clave, PerfilAlcance Perfil)[] Estandar =
    [
        ("Programador Senior", PerfilAlcance.Global),
        // Las claves reales del token, no un nombre inventado: si el helper sembrara uno que
        // AuthManager no emite, las pruebas irían contra una configuración que no existe.
        ("Encargado Transportación", PerfilAlcance.Transportacion),
        ("Encargado transportacion y mecanica", PerfilAlcance.Transportacion),
        ("Administrador de Filial", PerfilAlcance.Filial),
        ("Asistente Administrativo", PerfilAlcance.Filial),
    ];

    public static AlcanceEnvios Crear(SistemaEnviosDbContext db, IUserContext usuario)
    {
        Sembrar(db);
        return new AlcanceEnvios(db, usuario, new MemoryCache(new MemoryCacheOptions()), NullLogger<AlcanceEnvios>.Instance);
    }

    /// <summary>
    /// Idempotente y respetuosa con lo que la prueba ya sembró: si un caso mapea una clave a un
    /// perfil distinto a propósito, ese mapeo manda.
    /// </summary>
    public static void Sembrar(SistemaEnviosDbContext db)
    {
        var nuevas = Estandar
            .Where(x => !db.PerfilesPorPosicion.Local.Any(y => y.Posicion == x.Clave)
                        && !db.PerfilesPorPosicion.Any(y => y.Posicion == x.Clave))
            .Select(x => new PerfilPosicion { Posicion = x.Clave, Perfil = (byte)x.Perfil })
            .ToArray();

        if (nuevas.Length == 0) return;
        db.PerfilesPorPosicion.AddRange(nuevas);
        db.SaveChanges();
    }
}
