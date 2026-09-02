-- Mapea cada Ubicacion de tipo Filial con el id de filial que AuthManager emite en el claim
-- "affiliate" (formato "30,SANTO DOMINGO (SEDE)"). Sin esta columna el alcance por filial no
-- puede resolverse: CodigoCentro es un codigo de negocio local, no el id de AuthManager.
USE SistemaEnviosDB;
GO
-- QUOTED_IDENTIFIER debe estar ON: el indice filtrado no se crea sin el.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Ubicaciones', 'FilialExternaId') IS NULL
    ALTER TABLE dbo.Ubicaciones ADD FilialExternaId INT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Ubicaciones')
      AND name = N'UX_Ubicaciones_FilialExternaId'
)
    EXEC sp_executesql N'
        CREATE UNIQUE INDEX UX_Ubicaciones_FilialExternaId
            ON dbo.Ubicaciones(FilialExternaId)
            WHERE FilialExternaId IS NOT NULL;';

-- ----------------------------------------------------------------------------------
-- MAPEO REAL. Completar con los ids de filial de AuthManager antes de ejecutar en QA.
-- El unico confirmado hasta ahora es 30 = SANTO DOMINGO (SEDE).
-- ----------------------------------------------------------------------------------
EXEC sp_executesql N'
    UPDATE dbo.Ubicaciones SET FilialExternaId = 30 WHERE CodigoCentro = N''FILIAL-PRINCIPAL'';
';

-- Ninguna filial activa debe quedar sin mapear: sus usuarios veran una lista vacia.
-- Tecnologia no lleva id: su alcance sale de la posicion, no del centro.
EXEC sp_executesql N'
IF EXISTS (SELECT 1 FROM dbo.Ubicaciones WHERE Tipo = 1 AND Activo = 1 AND FilialExternaId IS NULL)
    THROW 51001, ''Hay filiales activas sin FilialExternaId. Complete el mapeo con AuthManager antes de continuar.'', 1;
';

COMMIT TRANSACTION;
GO
