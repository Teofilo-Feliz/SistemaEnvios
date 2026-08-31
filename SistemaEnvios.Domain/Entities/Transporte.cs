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
        public int TipoTransporteId { get; set; }
        public string? Observaciones { get; set; }

        public Envio Envio { get; set; } = null!;
        public TipoTransporte TipoTransporte { get; set; } = null!;
        public TransporteInterno? Interno { get; set; }
    public TransportePrivado? Privado { get; set; }
    public byte[] RowVersion { get; set; } = [];
        public ICollection<Incidencia> Incidencias { get; set; } = [];


    }
}
