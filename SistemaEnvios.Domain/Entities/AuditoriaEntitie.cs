using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public abstract class AuditoriaEntitie
    {
        public DateTime FechaCreacion { get; set; }

        public Guid? UsuarioCreacionId { get; set; }

        public DateTime? FechaModificacion { get; set; }

        public Guid? UsuarioModificacionId { get; set; }

    }
}
