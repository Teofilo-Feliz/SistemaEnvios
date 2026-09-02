using System.Security.Claims;
using SistemaEnvios.Application.Interfaces.Security;

namespace SistemaEnvios.Api.Security;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public string? Subject => User.FindFirstValue("sub");
    public Guid? UserId => Guid.TryParse(Subject, out var userId) ? userId : null;
    public string? Email => User.FindFirstValue("email");
    public string? Name => User.FindFirstValue("name");
    public string? Position => User.FindFirstValue("position");
    public string? Affiliate => User.FindFirstValue("affiliate");
    public int? AffiliateId => int.TryParse(Affiliate?.Split(',', 2)[0].Trim(), out var id) ? id : null;
    public IReadOnlyCollection<string> Roles => GetValues("roles");
    public IReadOnlyCollection<string> Permissions => GetValues("permissions");

    private IReadOnlyCollection<string> GetValues(string type) => User.FindAll(type).SelectMany(x => x.Value.Split
    (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
}
