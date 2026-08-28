using SistemaEnvios.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class Recepcion : AuditoriaEntitie
    {
        public int RecepcionId { get; set; }
        public int EnvioId { get; set; }
        public int? TecnicoAsignadoId { get; set; }
        public Guid? UsuarioQueRecibioId { get; set; }
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? FechaRecepcion { get; set; }
        public EstadoRecepcionEnum EstadoRecepcion { get; set; }
        public string? Observaciones { get; set; }
        public Guid UsuarioQueAsignoId { get; set; }

        public Envio Envio { get; set; } = null!;
        public ICollection<RecepcionEquipo> Equipos { get; set; } = [];


    }
}
