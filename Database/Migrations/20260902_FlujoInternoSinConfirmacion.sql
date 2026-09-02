/*
    Simplificacion del flujo interno (transportacion institucional) filial -> Tecnologia.

    Antes  EN_FILIAL -> ENTREGADO_TRANSPORTACION -> PENDIENTE_CONFIRMACION_TRANSPORTE
           -> CONFIRMADO_TRANSPORTACION -> EN_TRANSITO -> RECIBIDO_TRANSPORTACION
           -> ESPERA_TECNOLOGIA -> EN_REVISION -> RECIBIDO_TECNOLOGIA

    Ahora  EN_FILIAL -> ENTREGADO_TRANSPORTACION -> EN_TRANSITO
           -> RECIBIDO_TRANSPORTACION -> RECIBIDO_TECNOLOGIA

    El flujo PRIVADO no cambia y conserva ESPERA_TECNOLOGIA y EN_REVISION:
           EN_FILIAL -> DESPACHADO_TRANSPORTE_PRIVADO -> EN_TRANSITO
           -> ESPERA_TECNOLOGIA -> EN_REVISION -> RECIBIDO_TECNOLOGIA

    Los estados de confirmacion solo los usaba el flujo interno, asi que se desactivan.
    No se borran: el historial de envios anteriores los referencia por clave foranea.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Ningun envio puede quedar varado en un estado que deja de tener salida.
IF EXISTS (
    SELECT 1 FROM dbo.Envios v
    JOIN dbo.EstadosEnvio e ON e.EstadoEnvioId = v.EstadoEnvioId
    WHERE e.Codigo IN (N'PENDIENTE_CONFIRMACION_TRANSPORTE', N'CONFIRMADO_TRANSPORTACION')
)
    THROW 51002, 'Hay envios en un estado de confirmacion que se elimina. Avancelos antes de migrar.', 1;

DECLARE @Entregado INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'ENTREGADO_TRANSPORTACION');
DECLARE @Transito  INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'EN_TRANSITO');
DECLARE @RecTrans  INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'RECIBIDO_TRANSPORTACION');
DECLARE @RecTecno  INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'RECIBIDO_TECNOLOGIA');

IF @Entregado IS NULL OR @Transito IS NULL OR @RecTrans IS NULL OR @RecTecno IS NULL
    THROW 51003, 'Faltan estados del flujo interno. Revise el catalogo EstadosEnvio.', 1;

-- Transiciones del flujo viejo que dejan de existir.
DELETE t
FROM dbo.TransicionesEstadoEnvio t
JOIN dbo.EstadosEnvio o ON o.EstadoEnvioId = t.EstadoOrigenId
JOIN dbo.EstadosEnvio d ON d.EstadoEnvioId = t.EstadoDestinoId
WHERE (o.Codigo = N'ENTREGADO_TRANSPORTACION'          AND d.Codigo = N'PENDIENTE_CONFIRMACION_TRANSPORTE')
   OR (o.Codigo = N'PENDIENTE_CONFIRMACION_TRANSPORTE' AND d.Codigo = N'CONFIRMADO_TRANSPORTACION')
   OR (o.Codigo = N'CONFIRMADO_TRANSPORTACION'         AND d.Codigo = N'EN_TRANSITO')
   OR (o.Codigo = N'RECIBIDO_TRANSPORTACION'           AND d.Codigo = N'ESPERA_TECNOLOGIA');

-- Los dos saltos nuevos que reemplazan a los cuatro anteriores.
IF NOT EXISTS (SELECT 1 FROM dbo.TransicionesEstadoEnvio WHERE EstadoOrigenId = @Entregado AND EstadoDestinoId = @Transito)
    INSERT dbo.TransicionesEstadoEnvio (EstadoOrigenId, EstadoDestinoId, Activo) VALUES (@Entregado, @Transito, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.TransicionesEstadoEnvio WHERE EstadoOrigenId = @RecTrans AND EstadoDestinoId = @RecTecno)
    INSERT dbo.TransicionesEstadoEnvio (EstadoOrigenId, EstadoDestinoId, Activo) VALUES (@RecTrans, @RecTecno, 1);

-- Se desactivan, no se borran: HistorialEstadosEnvio los referencia.
UPDATE dbo.EstadosEnvio
   SET Activo = 0
 WHERE Codigo IN (N'PENDIENTE_CONFIRMACION_TRANSPORTE', N'CONFIRMADO_TRANSPORTACION');

COMMIT TRANSACTION;
GO

SELECT o.Codigo AS Origen, d.Codigo AS Destino
FROM dbo.TransicionesEstadoEnvio t
JOIN dbo.EstadosEnvio o ON o.EstadoEnvioId = t.EstadoOrigenId
JOIN dbo.EstadosEnvio d ON d.EstadoEnvioId = t.EstadoDestinoId
WHERE t.Activo = 1
ORDER BY o.Codigo, d.Codigo;
GO
