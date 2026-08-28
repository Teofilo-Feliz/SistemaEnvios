using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class Transporte : AuditoriaEntitie
    {
        public int TransporteId { get; set; }
        public int EnvioId { get; set; }
        public string Tipo { get; set; } = null!;
        public string? NombreChofer { get; set; }
        public string? Placa { get; set; }
        public DateTime? FechaEntregaTransportacion { get; set; }
        public string? Observaciones { get; set; }
        public bool EntregaConfirmada { get; set; }
        public DateTime? FechaConfirmacionEntrega { get; set; }
        public Guid? UsuarioConfirmacionId { get; set; }

        public Envio Envio { get; set; } = null!;
        public ICollection<Incidencia> Incidencias { get; set; } = [];


    }
}
