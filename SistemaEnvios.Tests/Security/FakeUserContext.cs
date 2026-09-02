using SistemaEnvios.Application.Interfaces.Security;

namespace SistemaEnvios.Tests.Security;

internal sealed class FakeUserContext(
    Guid? userId,
    string? affiliate = null,
    IReadOnlyCollection<string>? roles = null,
    IReadOnlyCollection<string>? permissions = null,
    string? position = null) : IUserContext
{
    public bool IsAuthenticated => userId.HasValue;
    public Guid? UserId => userId;
    public string? Subject => userId?.ToString();
    public string? Email => null;
    public string? Name => null;
    public string? Position => position;
    public string? Affiliate => affiliate;

    // Misma regla que HttpUserContext: el claim llega como "30,SANTO DOMINGO (SEDE)".
    public int? AffiliateId =>
        int.TryParse(affiliate?.Split(',', 2)[0].Trim(), out var id) ? id : null;

    public IReadOnlyCollection<string> Roles => roles ?? [];
    public IReadOnlyCollection<string> Permissions => permissions ?? [];

    /// <summary>Usuario sin filial y con rol global: ve todos los envios, sin restriccion de alcance.</summary>
    public static FakeUserContext Global(Guid? userId) => new(userId, roles: ["AdministradorGlobal"]);
}
