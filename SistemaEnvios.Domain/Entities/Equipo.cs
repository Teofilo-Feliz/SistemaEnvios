using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class Equipo : AuditoriaEntitie
    {
        public int EquipoId { get; set; }
        public string? CodigoActivo { get; set; }
        public string? NumeroSerie { get; set; }
        public int TipoEquipoId { get; set; }
        public int UbicacionActualId { get; set; }
        public string Marca { get; set; } = null!;
        public string Modelo { get; set; } = null!;
        public string? Observaciones { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public TipoEquipo TipoEquipo { get; set; } = null!;
        public Ubicacion UbicacionActual { get; set; } = null!;
        public ICollection<EnvioEquipo> Envios { get; set; } = [];

    }
}
