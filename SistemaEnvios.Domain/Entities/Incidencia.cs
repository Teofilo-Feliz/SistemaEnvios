using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class Incidencia : AuditoriaEntitie
    {
        public int IncidenciaId { get; set; }
        public int EnvioId { get; set; }
        public int? EnvioEquipoId { get; set; }
        public int? TransporteId { get; set; }
        public string Descripcion { get; set; } = null!;

        public Envio Envio { get; set; } = null!;
        public EnvioEquipo? EnvioEquipo { get; set; }
        public Transporte? Transporte { get; set; }

    }
}
