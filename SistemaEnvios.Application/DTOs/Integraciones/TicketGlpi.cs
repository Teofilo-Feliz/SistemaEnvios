namespace SistemaEnvios.Application.DTOs.Integraciones;

/// <summary>
/// Lo que la mesa de ayuda sabe de un ticket: si existe y qué activos tiene asociados.
/// </summary>
/// <remarks>
/// Sale de una sola llamada, <c>GET /Ticket/{id}/Item_Ticket</c>, que responde las dos preguntas:
/// 404 cuando el ticket no existe, y la lista —posiblemente vacía— cuando sí. Antes se consultaba
/// <c>GET /Ticket/{id}</c> solo para saber si existía; esta la sustituye sin costar una llamada
/// más.
///
/// <see cref="Existe"/> es <c>false</c> únicamente cuando GLPI respondió que no lo tiene. Si GLPI
/// no contestó, esto no llega a construirse: el fallo sale como
/// <see cref="Common.ErrorType.ExternalService"/> para que "no existe" y "no pude preguntar"
/// nunca se confundan.
/// </remarks>
public sealed record TicketGlpi(bool Existe, IReadOnlyList<ItemDeTicketGlpi> Equipos)
{
    public static TicketGlpi NoEncontrado { get; } = new(false, []);

    /// <summary>
    /// Un ticket es usable cuando trae un equipo o ninguno. Con ninguno, la persona escribe los
    /// datos a mano; con uno, se autocompletan.
    /// </summary>
    /// <remarks>
    /// Con varios NO es usable, y es una política, no una limitación técnica: en ADRTrack el
    /// ticket identifica el caso de UN equipo, que viaja a Tecnología y vuelve con ese mismo
    /// número. El índice único UX_EnvioEquipos_TicketApertura lo sostiene en la base. Dejar pasar
    /// un ticket de varios equipos significaría que solo el primero podría usarlo y los demás
    /// chocarían al guardar, después de que alguien llenó el formulario.
    /// </remarks>
    public bool EsUsable => Equipos.Count <= 1;

    public ItemDeTicketGlpi? EquipoUnico => Equipos.Count == 1 ? Equipos[0] : null;
}

/// <summary>
/// Un activo colgado de un ticket. <paramref name="ItemType"/> dice de qué tabla sacarlo:
/// "Computer", "Monitor", "Printer". Es el discriminador que permite que monitores e impresoras
/// funcionen con el mismo código que las computadoras.
/// </summary>
public sealed record ItemDeTicketGlpi(string ItemType, int ItemsId);
