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

    public int? AffiliateId =>
        int.TryParse(affiliate?.Split(',', 2)[0].Trim(), out var id) ? id : null;

    public IReadOnlyCollection<string> Roles => roles ?? [];
    public IReadOnlyCollection<string> Permissions => permissions ?? [];

    /// <summary>Usuario sin filial y con rol global: ve todos los envios, sin restriccion de alcance.</summary>
    /// <summary>
    /// Actor con alcance Global. Se identifica por un ROL mapeado: la posición ya no concede
    /// alcance, porque es un cargo de recursos humanos que no se puede revocar desde AuthManager.
    /// </summary>
    public static FakeUserContext Global(Guid? userId) => new(userId, roles: ["Programador Senior"]);

    /// <summary>
    /// Usuario tal como llega de AuthManager: su posición, su filial y sus roles. El token trae
    /// los roles en un solo claim separado por comas, y un mismo usuario puede traer varios.
    /// </summary>
    public static FakeUserContext ConRoles(Guid? userId, string? position, int filial, params string[] roles) =>
        new(userId, affiliate: $"{filial},SANTO DOMINGO (SEDE)", roles: roles, position: position);
}