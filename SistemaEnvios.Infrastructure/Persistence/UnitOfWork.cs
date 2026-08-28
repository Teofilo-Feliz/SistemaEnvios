using Microsoft.EntityFrameworkCore.Storage;
using SistemaEnvios.Application.Interfaces.Repositories;

namespace SistemaEnvios.Infrastructure.Persistence;

public sealed class UnitOfWork(SistemaEnviosDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;
    public async Task BeginTransactionAsync(CancellationToken ct = default) => _transaction = await db.Database.BeginTransactionAsync(ct);
    public async Task CommitTransactionAsync(CancellationToken ct = default) { if (_transaction is not null) { await db.SaveChangesAsync(ct); await _transaction.CommitAsync(ct); await _transaction.DisposeAsync(); _transaction = null; } }
    public async Task RollbackTransactionAsync(CancellationToken ct = default) { if (_transaction is not null) { await _transaction.RollbackAsync(ct); await _transaction.DisposeAsync(); _transaction = null; } }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
