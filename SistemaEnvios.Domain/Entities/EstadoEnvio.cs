using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SistemaEnvios.Domain.Entities
{
    public class EstadoEnvio : AuditoriaEntitie
    {
        public int EstadoEnvioId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public bool EsFinal { get; set; }
        public bool Activo { get; set; }

        public ICollection<Envio> Envios { get; set; } = [];
        public ICollection<TransicionEstadoEnvio> TransicionesOrigen { get; set; } = [];
        public ICollection<TransicionEstadoEnvio> TransicionesDestino { get; set; } = [];

    }
}
