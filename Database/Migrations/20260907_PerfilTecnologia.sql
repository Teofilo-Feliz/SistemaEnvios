-- Perfil 4 = Tecnología (soporte técnico).
--
-- Hasta ahora Tecnología y el administrador compartían el perfil Global: mismo alcance de datos
-- y, por tanto, el mismo módulo de entrada. Soporte técnico debe entrar directo a /tecnologia y
-- trabajar solo ahí, así que necesita un perfil propio. El alcance de DATOS no cambia —sigue
-- viendo todo, porque Tecnología está en un extremo de cada envío y es la única que descarta
-- equipos—; lo que cambia es qué pantallas puede abrir.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1) El CHECK admitía solo (1,2,3).
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PerfilPosicion_Perfil')
    ALTER TABLE dbo.PerfilesPorPosicion DROP CONSTRAINT CK_PerfilPosicion_Perfil;
GO

ALTER TABLE dbo.PerfilesPorPosicion
    ADD CONSTRAINT CK_PerfilPosicion_Perfil CHECK (Perfil IN (1, 2, 3, 4));
GO

-- 2) Mapee aquí la posición o el rol exacto que AuthManager emite para soporte técnico.
--    El valor tiene que coincidir letra por letra, acentos incluidos: la colación de SQL Server
--    distingue acentos y una tilde de más deja al usuario cayendo en el perfil de respaldo.
--    Si no lo conoce, entre una vez con ese usuario y búsquelo en el log del API:
--    "Perfil sin mapear: posición '...'; roles ..." — o léalo en la pantalla /unauthorized.
DECLARE @PosicionSoporteTecnico NVARCHAR(150) = NULL;   -- <-- p. ej. N'Soporte Técnico'

IF @PosicionSoporteTecnico IS NULL
BEGIN
    PRINT N'Defina @PosicionSoporteTecnico antes de ejecutar el paso 2. El CHECK ya admite el perfil 4.';
    RETURN;
END

MERGE dbo.PerfilesPorPosicion AS destino
USING (SELECT @PosicionSoporteTecnico AS Posicion, CAST(4 AS TINYINT) AS Perfil) AS origen
    ON destino.Posicion = origen.Posicion
WHEN MATCHED THEN UPDATE SET Perfil = origen.Perfil
WHEN NOT MATCHED THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, origen.Perfil);
GO

-- 3) Permisos de soporte técnico. Sin filas aquí el usuario entra sin poder hacer nada y todas
--    las pantallas responden 403. Se dejan fuera a propósito 'catalogos.administrar' y
--    'transportes.administrar': administrar el sistema y la flota no es su trabajo.
DECLARE @PosicionSoporteTecnico NVARCHAR(150) = NULL;   -- <-- el mismo valor del paso 2

IF @PosicionSoporteTecnico IS NULL RETURN;

MERGE dbo.PermisosPorPosicion AS destino
USING (
    SELECT @PosicionSoporteTecnico AS Posicion, Permiso
    FROM (VALUES
        (N'envios.consultar'),
        (N'envios.crear'),
        (N'envios.editar'),
        (N'envios.despachar'),
        (N'equipos.gestionar'),
        (N'recepciones.gestionar'),
        (N'incidencias.gestionar')
    ) AS p(Permiso)
) AS origen
    ON destino.Posicion = origen.Posicion AND destino.Permiso = origen.Permiso
WHEN NOT MATCHED THEN INSERT (Posicion, Permiso) VALUES (origen.Posicion, origen.Permiso);
GO
