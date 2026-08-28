namespace SistemaEnvios.Application.Interfaces.Security;

public interface IUserContext
{
    bool IsAuthenticated { get; }
    string? Subject { get; }
    string? Email { get; }
    string? Name { get; }
    string? Position { get; }
    string? Affiliate { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
}
