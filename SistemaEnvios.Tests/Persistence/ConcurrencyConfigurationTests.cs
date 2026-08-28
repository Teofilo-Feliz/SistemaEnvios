using Microsoft.EntityFrameworkCore;
using SistemaEnvios.Domain.Entities;
using SistemaEnvios.Infrastructure.Persistence;

namespace SistemaEnvios.Tests.Persistence;

public sealed class ConcurrencyConfigurationTests
{
    [Theory]
    [InlineData(typeof(Envio))]
    [InlineData(typeof(Equipo))]
    public void RowVersion_EstaConfiguradoComoTokenDeConcurrencia(Type entityType)
    {
        using var db = new SistemaEnviosDbContext(
            new DbContextOptionsBuilder<SistemaEnviosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var property = db.Model.FindEntityType(entityType)?.FindProperty("RowVersion");

        Assert.NotNull(property);
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }
}
