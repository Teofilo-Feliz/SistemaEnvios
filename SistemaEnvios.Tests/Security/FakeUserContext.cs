using SistemaEnvios.Application.Interfaces.Security;

namespace SistemaEnvios.Tests.Security;

internal sealed class FakeUserContext(Guid? userId) : IUserContext
{
    public bool IsAuthenticated => userId.HasValue;
    public Guid? UserId => userId;
    public string? Subject => userId?.ToString();
    public string? Email => null;
    public string? Name => null;
    public string? Position => null;
    public string? Affiliate => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
}
