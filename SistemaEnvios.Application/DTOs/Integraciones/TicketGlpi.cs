namespace SistemaEnvios.Application.DTOs.Integraciones;

/// <summary>
/// Lo que la mesa de ayuda sabe de un ticket: si existe, en qué estado está y qué activos tiene
/// asociados.
/// </summary>
/// <remarks>
/// Sale de dos llamadas que se hacen a la vez: <c>GET /Ticket/{id}</c> —existencia y estado— y
/// <c>GET /Ticket/{id}/Item_Ticket</c> —los activos—. La primera se había quitado cuando lo único
/// que hacía era confirmar que el ticket existía, algo que Item_Ticket ya respondía con su 404;
/// volvió cuando el estado pasó a ser parte de la regla, porque Item_Ticket no lo trae.
///
/// <see cref="Existe"/> es <c>false</c> únicamente cuando GLPI respondió que no lo tiene. Si GLPI
/// no contestó, esto no llega a construirse: el fallo sale como
/// <see cref="Common.ErrorType.ExternalService"/> para que "no existe" y "no pude preguntar"
/// nunca se confundan.
/// </remarks>
public sealed record TicketGlpi(bool Existe, IReadOnlyList<ItemDeTicketGlpi> Equipos, int? Estado = null)
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

    /// <summary>
    /// Si el estado que trae el ticket es motivo para rechazarlo.
    /// </summary>
    /// <remarks>
    /// Estado nulo es "no se pudo leer", no "está mal", y por eso no bloquea: misma política que
    /// el resto de la integración, donde una mesa de ayuda que no responde deja pasar en vez de
    /// parar a las 34 filiales. Solo se rechaza lo que GLPI afirmó.
    /// </remarks>
    public bool ElEstadoLoImpide => Estado.HasValue && Estado.Value != EstadoTicketGlpi.EnCurso;
}

/// <summary>
/// Los estados de un ticket de GLPI y cuál de ellos deja usarlo en un envío.
/// </summary>
/// <remarks>
/// Solo "En curso" sirve. La regla es de la ADR, no de GLPI: el equipo se manda a Tecnología
/// cuando ya hay un técnico ocupándose del caso, así que un ticket cerrado —o uno que nadie ha
/// tomado todavía— no puede estrenar un envío.
///
/// Esto aplica únicamente al ticket que alguien escribe a mano, que es el que abre un caso. El
/// equipo que vuelve de Tecnología hereda el ticket de nuestra base y nunca se vuelve a consultar
/// en GLPI, así que puede regresar a su filial aunque el caso ya se haya cerrado allá.
/// </remarks>
public static class EstadoTicketGlpi
{
    public const int EnCurso = 2;

    /// <summary>
    /// El nombre que la persona ve en GLPI. El mensaje de rechazo lo usa porque un número suelto
    /// no le dice nada a quien tiene el ticket abierto en pantalla.
    /// </summary>
    public static string Nombre(int? estado) => estado switch
    {
        1 => "Nuevo",
        2 => "En curso",
        3 => "En curso (planificado)",
        4 => "En espera",
        5 => "Resuelto",
        6 => "Cerrado",
        null => "sin estado",
        _ => $"desconocido ({estado})"
    };

    /// <summary>
    /// El rechazo, escrito una sola vez. Lo usan la búsqueda del formulario y la validación al
    /// guardar: si cada una dijera algo distinto, la persona creería que son dos problemas.
    /// </summary>
    public static string Mensaje(int ticket, int? estado) =>
        $"El ticket {ticket} está en estado «{Nombre(estado)}» en la mesa de ayuda y solo se " +
        "pueden usar tickets en curso. Verifique que el ticket esté asignado a un técnico y " +
        "vuelva a intentarlo.";
}

/// <summary>
/// Un activo colgado de un ticket. <paramref name="ItemType"/> dice de qué tabla sacarlo:
/// "Computer", "Monitor", "Printer". Es el discriminador que permite que monitores e impresoras
/// funcionen con el mismo código que las computadoras.
/// </summary>
public sealed record ItemDeTicketGlpi(string ItemType, int ItemsId);
