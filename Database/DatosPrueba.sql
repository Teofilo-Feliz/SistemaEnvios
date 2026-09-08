/*
    Datos de PRUEBA para recorrer el flujo. No se ejecuta en una instalacion real: los datos
    operativos son las ubicaciones (script base) y los choferes (Choferes.sql).

    Cubre las tres tablas sin las cuales el flujo no se puede recorrer completo:
      - Equipos             : un envio sin equipos no se puede crear ni despachar.
      - UsuariosReferencia  : requerido para asignar tecnico en la recepcion.

    Idempotente: se puede ejecutar varias veces sin duplicar.

    NOTA: los tecnicos de UsuariosReferencia son provisionales. Esa tabla deberia poblarse
    sincronizando desde AuthManager y hoy no existe ninguna sincronizacion: solo se lee.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Los choferes ya no viven aqui: son datos operativos reales y estan en Choferes.sql,
-- que se ejecuta despues del script base. Este archivo es solo para datos de prueba.

-- ---------------------------------------------------------------- Equipos
-- Repartidos entre cuatro filiales (para el flujo filial -> Tecnologia) y Tecnologia
-- (para el flujo de retorno Tecnologia -> filial).
INSERT INTO dbo.Equipos (CodigoActivo, NumeroSerie, TipoEquipoId, UbicacionActualId, Marca, Modelo, Observaciones)
SELECT e.CodigoActivo, e.NumeroSerie, te.TipoEquipoId, u.UbicacionId, e.Marca, e.Modelo, e.Observaciones
FROM (VALUES
    (N'ADR-001', N'SN-DL-77120', N'Laptop',                    N'SANTIAGO',           N'Dell',    N'Latitude 5440',  N'Equipo de recepción'),
    (N'ADR-002', N'SN-DL-77121', N'Laptop',                    N'SANTIAGO',           N'Dell',    N'Latitude 5440',  N'Equipo de caja'),
    (N'ADR-003', N'SN-HP-45012', N'Impresora',                 N'SANTIAGO',           N'HP',      N'LaserJet M404',  N'Impresora de archivo'),
    (N'ADR-004', N'SN-LN-33450', N'Computadora de escritorio', N'SANTO-DOMINGO-ESTE', N'Lenovo',  N'ThinkCentre M70',N'Consultorio 2'),
    (N'ADR-005', N'SN-LN-33451', N'Computadora de escritorio', N'SANTO-DOMINGO-ESTE', N'Lenovo',  N'ThinkCentre M70',N'Consultorio 3'),
    (N'ADR-006', N'SN-SM-90881', N'Monitor',                   N'SANTO-DOMINGO-ESTE', N'Samsung', N'S24R350',        N'Monitor de reemplazo'),
    (N'ADR-007', N'SN-DL-77122', N'Laptop',                    N'LA-VEGA',            N'Dell',    N'Vostro 3520',    N'Administración'),
    (N'ADR-008', N'SN-HP-45013', N'Impresora',                 N'LA-VEGA',            N'HP',      N'LaserJet M404',  N'Recepción'),
    (N'ADR-009', N'SN-LN-33452', N'Computadora de escritorio', N'SAN-CRISTOBAL',      N'Lenovo',  N'ThinkCentre M70',N'Facturación'),
    (N'ADR-010', N'SN-SM-90882', N'Monitor',                   N'SAN-CRISTOBAL',      N'Samsung', N'S24R350',        N'Facturación'),
    (N'ADR-011', N'SN-GN-10001', N'Otro',                      N'SAN-CRISTOBAL',      N'APC',     N'Back-UPS 650',   N'UPS con batería agotada'),
    (N'ADR-012', N'SN-DL-77123', N'Laptop',                    N'TECNOLOGIA',         N'Dell',    N'Latitude 5440',  N'Reparado, listo para devolver'),
    (N'ADR-013', N'SN-LN-33453', N'Computadora de escritorio', N'TECNOLOGIA',         N'Lenovo',  N'ThinkCentre M70',N'Equipo nuevo para asignar'),
    (N'ADR-014', N'SN-SM-90883', N'Monitor',                   N'TECNOLOGIA',         N'Samsung', N'S24R350',        N'Equipo nuevo para asignar'),
    (N'ADR-015', N'SN-HP-45014', N'Impresora',                 N'TECNOLOGIA',         N'HP',      N'LaserJet M404',  N'Reparada, lista para devolver')
) AS e (CodigoActivo, NumeroSerie, TipoNombre, CodigoCentro, Marca, Modelo, Observaciones)
JOIN dbo.TiposEquipo  te ON te.Nombre = e.TipoNombre
JOIN dbo.Ubicaciones  u  ON u.CodigoCentro = e.CodigoCentro
WHERE NOT EXISTS (SELECT 1 FROM dbo.Equipos x WHERE x.CodigoActivo = e.CodigoActivo);

-- ---------------------------------------------------------------- Tecnicos (provisional)
MERGE dbo.UsuariosReferencia AS destino
USING (VALUES
    ('b7d1a2c4-5e60-4f83-9a11-2c3d4e5f6071', N'Laura Méndez Polanco',  N'TEC-1001', N'lmendez@rehabilitacion.org.do',  1, 1),
    ('c8e2b3d5-6f71-4094-8b22-3d4e5f607182', N'Roberto Díaz Vásquez',  N'TEC-1002', N'rdiaz@rehabilitacion.org.do',    1, 1),
    ('d9f3c4e6-7082-41a5-9c33-4e5f60718293', N'Yuderka Santana Cruz',  N'TEC-1003', N'ysantana@rehabilitacion.org.do', 1, 1),
    ('e0a4d5f7-8193-42b6-8d44-5f6071829304', N'Técnico dado de baja',  N'TEC-0099', NULL,                             1, 0)
) AS origen (UsuarioExternoId, NombreCompleto, NumeroEmpleado, Correo, EsTecnico, Activo)
ON destino.UsuarioExternoId = origen.UsuarioExternoId
WHEN NOT MATCHED BY TARGET THEN
    INSERT (UsuarioExternoId, NombreCompleto, NumeroEmpleado, Correo, EsTecnico, Activo, FechaUltimaSincronizacion, Origen)
    VALUES (origen.UsuarioExternoId, origen.NombreCompleto, origen.NumeroEmpleado, origen.Correo,
            origen.EsTecnico, origen.Activo, SYSUTCDATETIME(), N'MANUAL');


-- ---------------------------------------------------------------- Casos de ticket
/*
    Escenarios para probar la herencia de ticket y el descarte. El ticket pertenece al caso,
    no al viaje: mientras el caso siga abierto, cada movimiento del equipo lo hereda.

      1001  ADR-012  caso ABIERTO, 1 movimiento  -> la devolucion debe heredar 1001 y solo
                                                    puede ir a Santiago. Sale en Descartes.
      1002  ADR-015  caso ABIERTO, 3 movimientos -> volvio con incidencia y se reenvio; sirve
                                                    para ver el contador de vueltas.
      1003  ADR-009  caso CERRADO conforme       -> el equipo puede estrenar ticket nuevo.

    ADR-013 y ADR-014 quedan en Tecnologia SIN caso: son asignaciones iniciales y por eso no
    deben aparecer en la pantalla de descarte.
*/
DECLARE @Usuario UNIQUEIDENTIFIER = '11111111-2222-3333-4444-555555555555';
DECLARE @Tecnologia INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'TECNOLOGIA');
DECLARE @Santiago   INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'SANTIAGO');
DECLARE @LaVega     INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'LA-VEGA');
DECLARE @SanCris    INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'SAN-CRISTOBAL');
DECLARE @EnTecnologia INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'RECIBIDO_TECNOLOGIA');
DECLARE @RecibidoFilial INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'RECIBIDO_FILIAL');

IF NOT EXISTS (SELECT 1 FROM dbo.EnvioEquipos WHERE NumeroTicket = N'1001')
BEGIN
    DECLARE @EnvioA INT, @AperturaA INT;

    -- 1001: Santiago mando el equipo y Tecnologia ya lo recibio. Caso abierto.
    INSERT INTO dbo.Envios (NumeroEnvio, UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    VALUES (N'ENV-2026-CASO1001', @Santiago, @Tecnologia, @EnTecnologia, 1, @Usuario, N'Laptop con fallo de teclado.', DATEADD(DAY, -6, SYSUTCDATETIME()));
    SET @EnvioA = SCOPE_IDENTITY();
    INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    SELECT @EnvioA, EquipoId, N'1001', @Usuario, N'Apertura del caso.', DATEADD(DAY, -6, SYSUTCDATETIME())
    FROM dbo.Equipos WHERE CodigoActivo = N'ADR-012';
    INSERT INTO dbo.HistorialesEstadoEnvio (EnvioId, EstadoEnvioId, Fecha, UbicacionId, UsuarioId, Observaciones)
    VALUES (@EnvioA, @EnTecnologia, DATEADD(DAY, -5, SYSUTCDATETIME()), @Tecnologia, @Usuario, N'Recibido por Tecnologia.');

    -- 1002: La Vega. Fue, volvio con incidencia y se reenvio: tres movimientos, mismo ticket.
    DECLARE @EnvioB INT, @EnvioB2 INT, @EnvioB3 INT, @AperturaB INT;
    INSERT INTO dbo.Envios (NumeroEnvio, UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    VALUES (N'ENV-2026-CASO1002A', @LaVega, @Tecnologia, @EnTecnologia, 1, @Usuario, N'Impresora no imprime.', DATEADD(DAY, -20, SYSUTCDATETIME()));
    SET @EnvioB = SCOPE_IDENTITY();
    INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    SELECT @EnvioB, EquipoId, N'1002', @Usuario, N'Apertura del caso.', DATEADD(DAY, -20, SYSUTCDATETIME())
    FROM dbo.Equipos WHERE CodigoActivo = N'ADR-015';
    SET @AperturaB = SCOPE_IDENTITY();

    INSERT INTO dbo.Envios (NumeroEnvio, UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    VALUES (N'ENV-2026-CASO1002B', @Tecnologia, @LaVega, @RecibidoFilial, 2, @Usuario, N'Devolucion tras reparacion.', DATEADD(DAY, -14, SYSUTCDATETIME()));
    SET @EnvioB2 = SCOPE_IDENTITY();
    INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, EnvioEquipoOrigenId, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    SELECT @EnvioB2, EquipoId, N'1002', @AperturaB, @Usuario, N'Devolucion: llego con incidencia.', DATEADD(DAY, -14, SYSUTCDATETIME())
    FROM dbo.Equipos WHERE CodigoActivo = N'ADR-015';

    INSERT INTO dbo.Envios (NumeroEnvio, UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    VALUES (N'ENV-2026-CASO1002C', @LaVega, @Tecnologia, @EnTecnologia, 1, @Usuario, N'Reenvio: el problema no se resolvio.', DATEADD(DAY, -9, SYSUTCDATETIME()));
    SET @EnvioB3 = SCOPE_IDENTITY();
    INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, EnvioEquipoOrigenId, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    SELECT @EnvioB3, EquipoId, N'1002', @AperturaB, @Usuario, N'Reenvio por incidencia.', DATEADD(DAY, -9, SYSUTCDATETIME())
    FROM dbo.Equipos WHERE CodigoActivo = N'ADR-015';

    -- 1003: ciclo completo cerrado conforme. El equipo ya volvio a San Cristobal.
    DECLARE @EnvioC INT, @AperturaC INT;
    INSERT INTO dbo.Envios (NumeroEnvio, UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion, FechaFinalizacion)
    VALUES (N'ENV-2026-CASO1003A', @SanCris, @Tecnologia, @EnTecnologia, 1, @Usuario, N'PC no enciende.', DATEADD(DAY, -30, SYSUTCDATETIME()), NULL);
    SET @EnvioC = SCOPE_IDENTITY();
    INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, UsuarioSolicitanteId, Observaciones, FechaCreacion, FechaCierreCaso, MotivoCierreCaso)
    SELECT @EnvioC, EquipoId, N'1003', @Usuario, N'Apertura del caso.', DATEADD(DAY, -30, SYSUTCDATETIME()),
           DATEADD(DAY, -22, SYSUTCDATETIME()), N'Recibido conforme en la filial.'
    FROM dbo.Equipos WHERE CodigoActivo = N'ADR-009';
    SET @AperturaC = SCOPE_IDENTITY();

    INSERT INTO dbo.Envios (NumeroEnvio, UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion, FechaFinalizacion)
    VALUES (N'ENV-2026-CASO1003B', @Tecnologia, @SanCris, @RecibidoFilial, 2, @Usuario, N'Devolucion reparada.', DATEADD(DAY, -24, SYSUTCDATETIME()), DATEADD(DAY, -22, SYSUTCDATETIME()));
    INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, EnvioEquipoOrigenId, UsuarioSolicitanteId, Observaciones, FechaCreacion)
    SELECT SCOPE_IDENTITY(), EquipoId, N'1003', @AperturaC, @Usuario, N'Devolucion recibida conforme.', DATEADD(DAY, -24, SYSUTCDATETIME())
    FROM dbo.Equipos WHERE CodigoActivo = N'ADR-009';
END

COMMIT TRANSACTION;
GO

SELECT 'Choferes activos'  AS Dato, COUNT(*) AS Total FROM dbo.ChoferesInternos WHERE Activo = 1
UNION ALL SELECT 'Choferes inactivos', COUNT(*) FROM dbo.ChoferesInternos WHERE Activo = 0
UNION ALL SELECT 'Tecnicos activos',   COUNT(*) FROM dbo.UsuariosReferencia WHERE EsTecnico = 1 AND Activo = 1
UNION ALL SELECT 'Equipos totales',    COUNT(*) FROM dbo.Equipos;
GO

SELECT u.Nombre AS Ubicacion, COUNT(*) AS Equipos
FROM dbo.Equipos e JOIN dbo.Ubicaciones u ON u.UbicacionId = e.UbicacionActualId
GROUP BY u.Nombre ORDER BY COUNT(*) DESC;
GO
