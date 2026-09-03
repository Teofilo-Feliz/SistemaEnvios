using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
public class EnvioEquipo : AuditoriaEntitie
    {
        public int EnvioEquipoId { get; set; }
        public int EnvioId { get; set; }
        public int EquipoId { get; set; }
        public string NumeroTicket { get; set; } = null!;
        public Guid UsuarioSolicitanteId { get; set; }
        public string Observaciones { get; set; } = null!;

        /// <summary>
        /// Movimiento que abrió el caso. NULL identifica a la apertura, que es la única fila
        /// que puede estrenar un número de ticket; las continuaciones lo heredan de ella.
        /// </summary>
        public int? EnvioEquipoOrigenId { get; set; }

        /// <summary>
        /// Solo en la fila de apertura. El cierre se sella cuando ocurre en vez de deducirse en
        /// cada consulta: así "¿este equipo tiene caso abierto?" es una sola comparación.
        /// </summary>
        public DateTime? FechaCierreCaso { get; set; }
        public string? MotivoCierreCaso { get; set; }

        public Envio Envio { get; set; } = null!;
        public Equipo Equipo { get; set; } = null!;
        public EnvioEquipo? Origen { get; set; }
        public ICollection<EnvioEquipo> Continuaciones { get; set; } = [];
        public RecepcionEquipo? RecepcionEquipo { get; set; }
        public byte[] RowVersion { get; set; } = [];
        public ICollection<Incidencia> Incidencias { get; set; } = [];

    }
}
