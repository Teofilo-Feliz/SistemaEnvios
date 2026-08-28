using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Repositories.Envios;

public sealed class EnvioEquipoRepository(SistemaEnviosDbContext db) : IEnvioEquipoRepository
{
    public Task<bool> ExisteEnvioAsync(int envioId, CancellationToken ct = default) => db.Envios.AnyAsync(x => x.EnvioId == envioId, ct);
    public Task<bool> ExisteEquipoAsync(int equipoId, CancellationToken ct = default) => db.Equipos.AnyAsync(x => x.EquipoId == equipoId, ct);
    public Task<bool> ExisteEnEnvioAsync(int envioId, int equipoId, CancellationToken ct = default) => db.EnvioEquipos.AnyAsync(x => x.EnvioId == envioId && x.EquipoId == equipoId, ct);
    public Task AgregarAsync(EnvioEquipo envioEquipo, CancellationToken ct = default) { db.EnvioEquipos.Add(envioEquipo); return Task.CompletedTask; }
}
