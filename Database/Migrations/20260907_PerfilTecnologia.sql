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

-- 2) Las claves de soporte técnico, tal como las emite AuthManager.
--
--    Se registran LAS DOS grafías a propósito. AuthManager envía la posición unas veces con
--    tilde y otras sin ella, y la colación de esta base (Modern_Spanish_CI_AS) ignora
--    mayúsculas pero DISTINGUE acentos: para SQL Server son dos claves diferentes. Con una sola
--    fila, la mitad del equipo entraría por el módulo equivocado y el síntoma sería "a unos les
--    funciona y a otros no", que es de los más difíciles de perseguir.
--
--    La tilde se escribe con NCHAR(233) y no como carácter literal para que la codificación con
--    la que se ejecute el script no guarde un carácter corrupto que luego no empareje.
DECLARE @ConTilde NVARCHAR(150) = N'Soporte T' + NCHAR(233) + N'cnico';
DECLARE @SinTilde NVARCHAR(150) = N'Soporte Tecnico';

MERGE dbo.PerfilesPorPosicion AS destino
USING (SELECT @ConTilde AS Posicion UNION ALL SELECT @SinTilde) AS origen
    ON destino.Posicion = origen.Posicion
WHEN MATCHED THEN UPDATE SET Perfil = 4
WHEN NOT MATCHED THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, 4);

-- 3) Permisos de soporte técnico. Sin filas aquí el usuario entra sin poder hacer nada y todas
--    las pantallas responden 403. Se dejan fuera a propósito 'catalogos.administrar' y
--    'transportes.administrar': administrar el sistema y la flota no es su trabajo.
MERGE dbo.PermisosPorPosicion AS destino
USING (
    SELECT p.Posicion, q.Permiso
    FROM (SELECT @ConTilde AS Posicion UNION ALL SELECT @SinTilde) AS p
    CROSS JOIN (VALUES
        (N'envios.consultar'),
        (N'envios.crear'),
        (N'envios.editar'),
        (N'envios.despachar'),
        (N'equipos.gestionar'),
        (N'recepciones.gestionar'),
        (N'incidencias.gestionar')
    ) AS q(Permiso)
) AS origen
    ON destino.Posicion = origen.Posicion AND destino.Permiso = origen.Permiso
WHEN NOT MATCHED THEN INSERT (Posicion, Permiso) VALUES (origen.Posicion, origen.Permiso);
GO

-- Comprobación: deben salir dos filas con Perfil = 4, y el carácter 10 de una debe ser 233 (é).
SELECT Posicion, Perfil, UNICODE(SUBSTRING(Posicion, 10, 1)) AS CodigoCaracter10
FROM dbo.PerfilesPorPosicion
WHERE Perfil = 4;
GO
