namespace SistemaEnvios.Application.DTOs.Envios;
public sealed record PaginaEnviosResponse(IReadOnlyCollection<EnvioResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
