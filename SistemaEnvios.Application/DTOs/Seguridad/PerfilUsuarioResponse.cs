namespace SistemaEnvios.Application.DTOs.Seguridad;

/// <summary>Quién es el usuario y qué alcance tiene, según lo decide el backend.</summary>
public sealed record PerfilUsuarioResponse(
    string Perfil,
    string? Posicion,
    int? FilialId,
    string? FilialNombre,
    int? UbicacionId,
    bool FilialMapeada,
    bool PuedeFiltrarPorFilial,
    IReadOnlyCollection<string> Permisos);
