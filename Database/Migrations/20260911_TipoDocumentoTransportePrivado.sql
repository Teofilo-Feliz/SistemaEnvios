-- El responsable de un transporte privado puede identificarse con cédula o con pasaporte.
--
-- Hasta ahora la columna se llamaba CedulaResponsable y la tabla exigía once dígitos exactos, así
-- que a un responsable extranjero no había forma de registrarlo: su pasaporte lleva letras y el
-- CHECK las rechazaba. Se agrega el tipo de documento y el CHECK pasa a depender de él.
--
-- La columna se renombra a DocumentoResponsable. Una columna llamada "cédula" que guarda
-- pasaportes es de las cosas que cuestan media hora entender cuando alguien lee esta tabla dentro
-- de un año.
--
-- Formatos, los mismos que aplica FormatosDocumento.cs en el código:
--
--   Cédula    -> exactamente 11 dígitos, sin letras ni guiones.
--   Pasaporte -> de 6 a 15 letras o dígitos, sin espacios ni guiones, en mayúsculas.
--
-- Las filas que ya existen son cédulas, así que el DEFAULT 1 las deja correctas sin tocarlas.
--
-- Idempotente: se puede volver a ejecutar sin efectos secundarios.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

-- 1) El tipo. Con DEFAULT para que las filas existentes queden como cédula, que es lo que son.
IF COL_LENGTH('dbo.TransportesPrivados', 'TipoDocumento') IS NULL
    ALTER TABLE dbo.TransportesPrivados
        ADD TipoDocumento TINYINT NOT NULL
            CONSTRAINT DF_TransportesPrivados_TipoDocumento DEFAULT (1);
GO

-- 2) El CHECK viejo exige once dígitos siempre; hay que quitarlo antes de tocar la columna.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_TransportesPrivados_Cedula')
    ALTER TABLE dbo.TransportesPrivados DROP CONSTRAINT CK_TransportesPrivados_Cedula;
GO

-- 3) El renombre, y el ancho nuevo: 15, que es el máximo del pasaporte. La cédula sigue acotada a
--    once por el CHECK, no por el tipo de la columna.
IF COL_LENGTH('dbo.TransportesPrivados', 'CedulaResponsable') IS NOT NULL
    EXEC sp_rename N'dbo.TransportesPrivados.CedulaResponsable', N'DocumentoResponsable', N'COLUMN';
GO

ALTER TABLE dbo.TransportesPrivados ALTER COLUMN DocumentoResponsable NVARCHAR(15) NOT NULL;
GO

-- 4) El CHECK nuevo, que depende del tipo.
--
--    Ojo con la colación: es Modern_Spanish_CI_AS, que ignora mayúsculas, así que '[^A-Z0-9]' deja
--    pasar minúsculas. Pero ojo con el rango: en LIKE, '[A-Z]' va en orden de INTERCALACIÓN, no
--    ASCII, y ese orden es a,A,b,B,c,C… así que 'A-Z' incluiría casi todas las minúsculas incluso
--    con una colación sensible a mayúsculas. Con Latin1_General_BIN2 el rango sí es el ASCII
--    65-90, y las minúsculas quedan fuera. El código sube el pasaporte a mayúsculas antes de
--    guardarlo; esto impide que la tabla acepte lo que el código nunca escribiría.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_TransportesPrivados_Documento')
    ALTER TABLE dbo.TransportesPrivados DROP CONSTRAINT CK_TransportesPrivados_Documento;
GO

ALTER TABLE dbo.TransportesPrivados ADD CONSTRAINT CK_TransportesPrivados_Documento CHECK
(
    (TipoDocumento = 1
        AND DocumentoResponsable NOT LIKE '%[^0-9]%'
        AND LEN(DocumentoResponsable) = 11)
 OR (TipoDocumento = 2
        AND DocumentoResponsable COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%'
        AND LEN(DocumentoResponsable) BETWEEN 6 AND 15)
);
GO

/* ------------------------------ Verificación ------------------------------ */

-- Esperado: TipoDocumento TINYINT NOT NULL, DocumentoResponsable NVARCHAR(15) NOT NULL, y ninguna
-- columna llamada CedulaResponsable.
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = N'TransportesPrivados'
  AND COLUMN_NAME IN (N'TipoDocumento', N'DocumentoResponsable', N'CedulaResponsable');
GO

-- Esperado: ninguna fila. Toda fila existente tiene que cumplir el CHECK nuevo; si alguna sale
-- aquí, el ALTER de arriba habría fallado y esta base quedó a medias.
SELECT TransporteId, TipoDocumento, DocumentoResponsable
FROM dbo.TransportesPrivados
WHERE NOT (
    (TipoDocumento = 1 AND DocumentoResponsable NOT LIKE '%[^0-9]%' AND LEN(DocumentoResponsable) = 11)
 OR (TipoDocumento = 2 AND DocumentoResponsable COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%' AND LEN(DocumentoResponsable) BETWEEN 6 AND 15));
GO

-- Los responsables registrados, para revisarlos de un vistazo.
SELECT TransporteId,
       CASE TipoDocumento WHEN 1 THEN 'Cédula' WHEN 2 THEN 'Pasaporte' END AS TipoDocumento,
       DocumentoResponsable, LEN(DocumentoResponsable) AS Largo
FROM dbo.TransportesPrivados ORDER BY TransporteId;
GO
