using System;
using System.Collections.Generic;
using System.Text;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Domain.Entities
{
    public class Envio : AuditoriaEntitie
    {
        public int EnvioId { get; set; }
        public string NumeroEnvio { get; set; } = string.Empty;
        public int UbicacionOrigenId { get; set; }
        public int UbicacionDestinoId { get; set; }
        public int EstadoEnvioId { get; set; }
        public DireccionEnvioEnum Direccion { get; set; }
        public Guid UsuarioSolicitanteId { get; set; }
        public DateTime? FechaFinalizacion { get; set; }
        public string? Observaciones { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public Ubicacion UbicacionOrigen { get; set; } = null!;
        public Ubicacion UbicacionDestino { get; set; } = null!;
        public EstadoEnvio EstadoEnvio { get; set; } = null!;
        public ICollection<EnvioEquipo> Equipos { get; set; } = [];
        public Transporte? Transporte { get; set; }
        public Recepcion? Recepcion { get; set; }
        public ICollection<Incidencia> Incidencias { get; set; } = [];
        public ICollection<HistorialEstadoEnvio> HistorialEstados { get; set; } = [];

    }
}
