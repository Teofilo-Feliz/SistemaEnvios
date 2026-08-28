using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class TipoEquipo : AuditoriaEntitie
    {
        public int TipoEquipoId { get; set; }
        public string Nombre { get; set; } = null!;
        public bool Activo { get; set; }

        public ICollection<Equipo> Equipos { get; set; } = [];

    }
}
