using System;
using System.Collections.Generic;
using System.Text;

namespace SistemaEnvios.Domain.Enums
{
    public enum EstadoRecepcionEnum
    {
        Pendiente = 1,
        Asignada = 2,
        EnProceso = 3,
        Completada = 4,
        CompletadaConIncidencia = 5,

    }
}
