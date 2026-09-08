/*
    Choferes internos de Transportación.

    Va aparte de DatosPrueba.sql a proposito: los choferes son datos operativos reales, no de
    prueba. Se ejecuta despues de SistemaEnviosDB.sql, que ya siembra las 34 filiales y
    Tecnologia, los estados del flujo y el acceso por posicion.

    NO siembra equipos: el inventario lo carga cada filial segun lo que tiene.

    Idempotente: se puede volver a ejecutar sin duplicar.
*/
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

MERGE dbo.ChoferesInternos AS destino
USING (VALUES
    (N'Ramón Castillo Peña',     N'EMP-4101', 1),
    (N'José Luis Encarnación',   N'EMP-4102', 1),
    (N'Wilkin Rosario Núñez',    N'EMP-4103', 1),
    (N'Ángel Manuel Frías',      N'EMP-4104', 1),
    (N'Domingo Reyes Cabrera',   N'EMP-4105', 1),
    (N'Franklin Ureña Santos',   N'EMP-4106', 0)   -- inactivo: no debe poder asignarse
) AS origen (NombreCompleto, NumeroEmpleado, Activo)
ON destino.NumeroEmpleado = origen.NumeroEmpleado
WHEN NOT MATCHED BY TARGET THEN
    INSERT (NombreCompleto, NumeroEmpleado, Activo)
    VALUES (origen.NombreCompleto, origen.NumeroEmpleado, origen.Activo);

COMMIT TRANSACTION;
GO

SELECT 'Filiales'          AS Dato, COUNT(*) AS Total FROM dbo.Ubicaciones WHERE Tipo = 1
UNION ALL SELECT 'Tecnologia',      COUNT(*) FROM dbo.Ubicaciones WHERE Tipo = 2
UNION ALL SELECT 'Choferes activos', COUNT(*) FROM dbo.ChoferesInternos WHERE Activo = 1
UNION ALL SELECT 'Choferes inactivos', COUNT(*) FROM dbo.ChoferesInternos WHERE Activo = 0
UNION ALL SELECT 'Equipos',          COUNT(*) FROM dbo.Equipos
UNION ALL SELECT 'Envios',           COUNT(*) FROM dbo.Envios;
GO
