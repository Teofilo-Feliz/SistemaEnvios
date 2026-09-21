/* ==============================================================================================
   Numero de envio: de GUID a ENV-AAAA-MM-######

   Reemplaza NumeroEnvio por una columna calculada PERSISTED. Los envios que ya existen quedan
   renumerados con el formato nuevo, derivado de su propia FechaCreacion y de su EnvioId.

   ES IDEMPOTENTE: si la columna ya es calculada, no hace nada y lo dice.

   OJO: los numeros anteriores SE PIERDEN. Antes de borrarlos los guarda en
   dbo.NumerosEnvioAnteriores y al final imprime la equivalencia viejo -> nuevo. Si alguien
   anoto un numero en papel, ahi esta a que envio corresponde.

   Ejecutar con QUOTED_IDENTIFIER ON: SQL Server lo exige para crear columnas calculadas
   persistidas. SSMS lo trae asi; sqlcmd -Q no, por eso se fija abajo.

   Y OJO DESPUES DE MIGRAR: esa exigencia no es solo de la creacion. Cualquier INSERT o UPDATE
   sobre dbo.Envios necesita QUOTED_IDENTIFIER ON, o SQL Server lo rechaza con el error 1934.
   El API no se entera —SqlClient lo activa por si mismo, y SSMS tambien—, pero un arreglo
   manual con "sqlcmd -Q" falla si no se pone antes:

       sqlcmd -S servidor -d ADRTrack -Q "SET QUOTED_IDENTIFIER ON; INSERT INTO dbo.Envios ..."
   ============================================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF EXISTS (SELECT 1 FROM sys.computed_columns
           WHERE object_id = OBJECT_ID(N'dbo.Envios') AND name = N'NumeroEnvio')
BEGIN
    PRINT N'NumeroEnvio ya es una columna calculada. No hay nada que migrar.';
END
ELSE
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Se guarda lo que habia. La tabla se conserva despues de la migracion a proposito:
        --    es el unico rastro de los numeros viejos.
        IF OBJECT_ID(N'dbo.NumerosEnvioAnteriores', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.NumerosEnvioAnteriores
            (
                EnvioId INT NOT NULL,
                NumeroAnterior NVARCHAR(30) NOT NULL,
                FechaMigracion DATETIME2(7) NOT NULL
                    CONSTRAINT DF_NumerosEnvioAnteriores_Fecha DEFAULT (SYSUTCDATETIME()),
                CONSTRAINT PK_NumerosEnvioAnteriores PRIMARY KEY (EnvioId)
            );
        END

        INSERT INTO dbo.NumerosEnvioAnteriores (EnvioId, NumeroAnterior)
        SELECT e.EnvioId, e.NumeroEnvio
        FROM dbo.Envios e
        WHERE NOT EXISTS (SELECT 1 FROM dbo.NumerosEnvioAnteriores a WHERE a.EnvioId = e.EnvioId);

        -- 2. La restriccion unica se apoya en la columna, asi que sale primero. El nombre se
        --    busca en el catalogo en vez de darlo por sentado: en una base creada por EF se
        --    llama distinto que en una creada por el script.
        DECLARE @restriccion SYSNAME;
        DECLARE @sql NVARCHAR(MAX);

        SELECT @restriccion = kc.name
        FROM sys.key_constraints kc
        JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE kc.parent_object_id = OBJECT_ID(N'dbo.Envios') AND c.name = N'NumeroEnvio';

        IF @restriccion IS NOT NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.Envios DROP CONSTRAINT ' + QUOTENAME(@restriccion) + N';';
            EXEC sp_executesql @sql;
        END

        DECLARE @indice SYSNAME;
        SELECT @indice = i.name
        FROM sys.indexes i
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Envios') AND c.name = N'NumeroEnvio' AND i.is_primary_key = 0
          AND i.is_unique_constraint = 0;

        IF @indice IS NOT NULL
        BEGIN
            SET @sql = N'DROP INDEX ' + QUOTENAME(@indice) + N' ON dbo.Envios;';
            EXEC sp_executesql @sql;
        END

        -- 3. Fuera la columna de datos, dentro la calculada. No hay forma de convertir una en
        --    otra: ALTER COLUMN no puede volver calculada una columna que no lo era.
        ALTER TABLE dbo.Envios DROP COLUMN NumeroEnvio;

        ALTER TABLE dbo.Envios ADD NumeroEnvio AS
            ('ENV-' + LEFT(CONVERT(VARCHAR(10), DATEADD(HOUR, -4, FechaCreacion), 126), 7)
             + '-' + RIGHT('000000' + CAST(EnvioId AS VARCHAR(10)), 6)) PERSISTED NOT NULL;

        ALTER TABLE dbo.Envios ADD CONSTRAINT UQ_Envios_NumeroEnvio UNIQUE (NumeroEnvio);

        COMMIT TRANSACTION;
        PRINT N'Migracion realizada. NumeroEnvio pasa a calcularse solo.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        PRINT N'ERROR: la migracion se deshizo entera y la tabla quedo como estaba.';
        THROW;
    END CATCH
END
GO

/* ----------------------------------------------------------------------------------------------
   Equivalencia, para quien tenga un numero viejo anotado.
   ---------------------------------------------------------------------------------------------- */
SELECT a.EnvioId,
       a.NumeroAnterior,
       e.NumeroEnvio AS NumeroNuevo,
       e.FechaCreacion AS FechaCreacionUtc,
       DATEADD(HOUR, -4, e.FechaCreacion) AS FechaCreacionLocal
FROM dbo.NumerosEnvioAnteriores a
JOIN dbo.Envios e ON e.EnvioId = a.EnvioId
ORDER BY a.EnvioId;
GO

/* ----------------------------------------------------------------------------------------------
   Comprobacion: ningun envio sin numero, ninguno repetido, y el formato exacto en todos.
   ---------------------------------------------------------------------------------------------- */
DECLARE @total INT, @distintos INT, @malFormados INT;

SELECT @total = COUNT(*),
       @distintos = COUNT(DISTINCT NumeroEnvio),
       @malFormados = SUM(CASE WHEN NumeroEnvio LIKE 'ENV-[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9][0-9][0-9][0-9][0-9]'
                               THEN 0 ELSE 1 END)
FROM dbo.Envios;

IF @total <> @distintos OR ISNULL(@malFormados, 0) > 0
    RAISERROR(N'La migracion dejo la tabla inconsistente: %d envios, %d numeros distintos, %d con formato invalido.',
              16, 1, @total, @distintos, @malFormados);
ELSE
    PRINT N'Comprobacion OK: ' + CAST(@total AS NVARCHAR(10)) + N' envios, todos con numero unico y bien formado.';
GO
