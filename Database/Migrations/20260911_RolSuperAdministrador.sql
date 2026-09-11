-- El grupo de seguridad SuperAdministrador de AuthManager, con alcance Global.
--
-- El token de un super administrador llega así:
--
--   "position": "Encargada de Soporte Técnico"
--   "roles":    "Soporte Técnico,SuperAdministrador"
--
-- Ninguna de esas dos claves resolvía a Global. La posición no estaba registrada, y de los dos
-- roles solo lo estaba "Soporte Técnico", así que el sistema la dejaba en el perfil de Tecnología:
-- veía los mismos datos pero sin los módulos de administración. Antes de que existiera esa fila
-- caía al respaldo "tiene affiliate, luego es Filial", que es el error de la filial 30 con el que
-- empezó todo.
--
-- IMPORTANTE: la posición "Encargada de Soporte Técnico" se deja SIN registrar a propósito. La
-- posición se consulta ANTES que los roles, así que mapearla fijaría su perfil para siempre y su
-- rol de SuperAdministrador no contaría nunca.
--
-- Va acompañada del cambio en AlcanceEnvios: cuando un usuario trae varias claves mapeadas gana la
-- de mayor alcance. Sin eso, esta fila convertiría un fallo constante en uno intermitente —la
-- consulta tomaba el primero que devolviera SQL Server, sin ORDER BY— que es peor.
--
-- Idempotente: se puede volver a ejecutar sin efectos secundarios.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

DECLARE @Rol NVARCHAR(150) = N'SuperAdministrador';

-- 1) El alcance: Global.
MERGE dbo.PerfilesPorPosicion AS destino
USING (SELECT @Rol AS Posicion) AS origen ON destino.Posicion = origen.Posicion
WHEN MATCHED AND destino.Perfil <> 1 THEN UPDATE SET Perfil = 1
WHEN NOT MATCHED THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, 1);

-- 2) Los permisos. Sin filas aquí el usuario entra con el alcance correcto y todas las pantallas
--    responden 403, porque el perfil dice SOBRE CUÁLES envíos y el permiso dice QUÉ puede hacer.
--    Son los mismos once de 'Programador Senior', que es el otro perfil Global.
MERGE dbo.PermisosPorPosicion AS destino
USING (
    SELECT @Rol AS Posicion, q.Permiso
    FROM (VALUES
        (N'envios.consultar'),
        (N'envios.crear'),
        (N'envios.editar'),
        (N'envios.despachar'),
        (N'equipos.gestionar'),
        (N'recepciones.gestionar'),
        (N'incidencias.gestionar'),
        (N'transportes.gestionar'),
        (N'transportes.confirmar'),
        (N'transportes.administrar'),
        (N'catalogos.administrar')
    ) AS q(Permiso)
) AS origen
    ON destino.Posicion = origen.Posicion AND destino.Permiso = origen.Permiso
WHEN NOT MATCHED THEN INSERT (Posicion, Permiso) VALUES (origen.Posicion, origen.Permiso);
GO

/* ------------------------------ Verificación ------------------------------ */

-- Esperado: SuperAdministrador con Perfil 1 y 11 permisos.
SELECT pf.Posicion,
       CASE pf.Perfil WHEN 1 THEN 'Global' WHEN 2 THEN 'Transportacion'
                      WHEN 3 THEN 'Filial' WHEN 4 THEN 'Tecnologia' END AS Perfil,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion p WHERE p.Posicion = pf.Posicion) AS Permisos
FROM dbo.PerfilesPorPosicion pf
WHERE pf.Posicion = N'SuperAdministrador';
GO

-- Esperado: 8 y 60. Es el conteo de una base al día.
SELECT (SELECT COUNT(*) FROM dbo.PerfilesPorPosicion) AS Perfiles,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion) AS Permisos;
GO

-- Esperado: ninguna fila. Una posición en una tabla y ausente en la otra es el estado que más
-- caro sale: no falla al insertarla, sino el día que alguien de ese grupo entra.
SELECT N'permisos sin perfil' AS Problema, pp.Posicion
FROM (SELECT DISTINCT Posicion FROM dbo.PermisosPorPosicion) AS pp
LEFT JOIN dbo.PerfilesPorPosicion AS pf ON pf.Posicion = pp.Posicion
WHERE pf.Posicion IS NULL
UNION ALL
SELECT N'perfil sin permisos', pf.Posicion
FROM dbo.PerfilesPorPosicion AS pf
LEFT JOIN dbo.PermisosPorPosicion AS pp ON pp.Posicion = pf.Posicion
WHERE pp.Posicion IS NULL;
GO
