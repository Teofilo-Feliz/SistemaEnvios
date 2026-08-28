using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Application.Interfaces.Repositories;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Infrastructure.Repositories;

public class GenericRepository<T>(SistemaEnviosDbContext context) : IGenericRepository<T> where T : class
{
    protected readonly SistemaEnviosDbContext Context = context;
    protected readonly DbSet<T> Set = context.Set<T>();
    public Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Set.FindAsync([id], cancellationToken).AsTask();
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) => Set.AnyAsync(e => EF.Property<int>(e, GetKeyName()) == id, cancellationToken);
    public Task AddAsync(T entity, CancellationToken cancellationToken = default) => Set.AddAsync(entity, cancellationToken).AsTask();
    public void Update(T entity) => Set.Update(entity);
    public void Remove(T entity) => Set.Remove(entity);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Context.SaveChangesAsync(cancellationToken);
    private static string GetKeyName() => typeof(T).Name + "Id";
}
