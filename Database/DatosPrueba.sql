/*
    Datos de prueba para el flujo de envios, alineados con las 34 filiales reales + Tecnologia.

    Cubre las tres tablas sin las cuales el flujo no se puede recorrer completo:
      - ChoferesInternos    : requerido para registrar transporte institucional.
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

-- ---------------------------------------------------------------- Choferes internos
MERGE dbo.ChoferesInternos AS destino
USING (VALUES
    (N'Ramón Castillo Peña',     N'EMP-4101', 1),
    (N'José Luis Encarnación',   N'EMP-4102', 1),
    (N'Wilkin Rosario Núñez',    N'EMP-4103', 1),
    (N'Ángel Manuel Frías',      N'EMP-4104', 1),
    (N'Domingo Reyes Cabrera',   N'EMP-4105', 1),
    (N'Franklin Ureña Santos',   N'EMP-4106', 0)   -- inactivo: para probar que no se puede asignar
) AS origen (NombreCompleto, NumeroEmpleado, Activo)
ON destino.NumeroEmpleado = origen.NumeroEmpleado
WHEN NOT MATCHED BY TARGET THEN
    INSERT (NombreCompleto, NumeroEmpleado, Activo)
    VALUES (origen.NombreCompleto, origen.NumeroEmpleado, origen.Activo);

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
