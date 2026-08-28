using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class HistorialEstadoEnvio : AuditoriaEntitie
    {
        public int HistorialEstadoEnvioId { get; set; }
        public int EnvioId { get; set; }
        public int EstadoEnvioId { get; set; }
        public DateTime Fecha { get; set; }
        public int UbicacionId { get; set; }
        public Guid UsuarioId { get; set; }
        public string? Observaciones { get; set; }

        public Envio Envio { get; set; } = null!;
        public EstadoEnvio EstadoEnvio { get; set; } = null!;
        public Ubicacion Ubicacion { get; set; } = null!;
    }
}
