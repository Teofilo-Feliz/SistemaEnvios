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

        public Envio Envio { get; set; } = null!;
        public Equipo Equipo { get; set; } = null!;
        public RecepcionEquipo? RecepcionEquipo { get; set; }
        public ICollection<Incidencia> Incidencias { get; set; } = [];

    }
}
