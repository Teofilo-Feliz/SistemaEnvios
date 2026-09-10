-- El envío por transporte privado se recibe en un solo paso, igual que el institucional.
--
-- El privado va de la filial directo a Tecnología y no pasa por Transportación, así que nadie
-- confirma su llegada por él: cuando Tecnología lo tiene delante, lo recibe. Hasta ahora tenía
-- que dar TRES pasos en tres pantallas para lo que en el institucional es uno:
--
--   EN_TRANSITO -> ESPERA_TECNOLOGIA -> EN_REVISION -> (conforme/incidencia) -> RECIBIDO_TECNOLOGIA
--
-- Y el paso del medio cambiaba el estado a ciegas, escribiendo en el historial "Equipos recibidos
-- y verificados por Tecnología" sin que nadie hubiera verificado nada. Eso ensuciaba la
-- trazabilidad, que es lo único que este sistema promete.
--
-- Con estas dos transiciones el privado se recibe directamente desde EN_TRANSITO, marcando cada
-- equipo conforme o con incidencia como en cualquier otra recepción.
--
-- El estado no basta para autorizarlo: un envío INSTITUCIONAL en EN_TRANSITO sigue en la
-- carretera y es de Transportación. Quien distingue es RecepcionService.PermiteVerificar, que
-- admite EN_TRANSITO solo cuando la estrategia es EntregaDirectaTecnologia. Estas filas abren el
-- camino; ese guardia decide quién puede tomarlo.
--
-- Idempotente: se puede volver a ejecutar sin efectos secundarios.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

INSERT INTO dbo.TransicionesEstadoEnvio (EstadoOrigenId, EstadoDestinoId, Activo)
SELECT origen.EstadoEnvioId, destino.EstadoEnvioId, 1
FROM (VALUES
    (N'EN_TRANSITO', N'RECIBIDO_TECNOLOGIA'),
    (N'EN_TRANSITO', N'RECIBIDO_TECNOLOGIA_INCIDENCIA')
) AS flujo(CodigoOrigen, CodigoDestino)
INNER JOIN dbo.EstadosEnvio origen  ON origen.Codigo  = flujo.CodigoOrigen
INNER JOIN dbo.EstadosEnvio destino ON destino.Codigo = flujo.CodigoDestino
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.TransicionesEstadoEnvio t
    WHERE t.EstadoOrigenId = origen.EstadoEnvioId
      AND t.EstadoDestinoId = destino.EstadoEnvioId);
GO

/* ------------------------------ Verificación ------------------------------ */

-- Esperado: seis filas. Las dos nuevas desde EN_TRANSITO, más las cuatro que ya existían desde
-- RECIBIDO_TRANSPORTACION y EN_REVISION, que son el camino del institucional. Cada uno de esos
-- tres orígenes llega a los dos desenlaces: conforme y con incidencia.
SELECT o.Codigo AS Desde, d.Codigo AS Hasta
FROM dbo.TransicionesEstadoEnvio t
JOIN dbo.EstadosEnvio o ON o.EstadoEnvioId = t.EstadoOrigenId
JOIN dbo.EstadosEnvio d ON d.EstadoEnvioId = t.EstadoDestinoId
WHERE d.Codigo IN (N'RECIBIDO_TECNOLOGIA', N'RECIBIDO_TECNOLOGIA_INCIDENCIA')
ORDER BY d.Codigo, o.Codigo;
GO
