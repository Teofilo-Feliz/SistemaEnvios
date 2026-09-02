using System;
using System.Collections.Generic;
using System.Text;
using SistemaEnvios.Domain.Enums;

namespace SistemaEnvios.Domain.Entities
{
    public class Ubicacion : AuditoriaEntitie
    {
        public int UbicacionId { get; set; }
        public string Nombre { get; set; } = null!;
        public string CodigoCentro { get; set; } = null!;
        /// <summary>Id de la filial en AuthManager (claim "affiliate"). NULL en ubicaciones que no son filial.</summary>
        public int? FilialExternaId { get; set; }
        public TipoUbicacionEnum Tipo { get; set; }
        public bool Activo { get; set; }

        public ICollection<Envio> EnviosOrigen { get; set; } = [];
        public ICollection<Envio> EnviosDestino { get; set; } = [];
        public ICollection<Equipo> EquiposActuales { get; set; } = [];
        public ICollection<HistorialEstadoEnvio> HistorialEstados { get; set; } = [];
    }
}
