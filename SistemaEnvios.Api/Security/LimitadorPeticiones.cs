namespace SistemaEnvios.Api.Security;

/// <summary>
/// Reparte el límite de peticiones por usuario. Un límite global lo consumiría el usuario más
/// activo y dejaría sin cupo a las demás filiales; por IP alcanza a quien todavía no se
/// autenticó, que es justo donde un abuso llega sin credenciales.
/// </summary>
public static class LimitadorPeticiones
{
    public const int PeticionesPorMinuto = 240;

    public static string Particion(HttpContext contexto)
    {
        var sujeto = contexto.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sujeto)) return $"sub:{sujeto}";
        return $"ip:{contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida"}";
    }
}
