using SistemaEnvios.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
public class RecepcionEquipo : AuditoriaEntitie
    {
        public int RecepcionEquipoId { get; set; }
        public int RecepcionId { get; set; }
        public int EnvioEquipoId { get; set; }
        public EstadoRecepcionEquipoEnum EstadoRecepcionEquipo { get; set; }
        public DateTime? FechaVerificacion { get; set; }
        public string? Observaciones { get; set; }

        public Recepcion Recepcion { get; set; } = null!;
        public EnvioEquipo EnvioEquipo { get; set; } = null!;
        public byte[] RowVersion { get; set; } = [];

    }
}
