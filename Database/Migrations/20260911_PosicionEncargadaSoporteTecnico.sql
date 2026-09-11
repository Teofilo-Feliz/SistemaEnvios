-- La posición "Encargada de Soporte Técnico" con alcance Global: es la dueña del sistema.
--
-- Su token llega con esta posición y con los roles "Soporte Técnico,SuperAdministrador", y ya
-- resolvía a Global por el segundo. Esta fila añade una segunda vía por su cargo, para que el
-- alcance no dependa solo de seguir en un grupo de AuthManager.
--
-- Antes de que posición y roles se miraran JUNTOS esto habría sido un error: la posición se
-- consultaba primero, así que mapearla a un perfil más estrecho habría tapado el rol de super
-- administradora. Ahora gana el de mayor alcance, y las tres claves apuntan a Global o por debajo,
-- así que añadirla no cambia lo que ya tiene: lo respalda.
--
-- OJO CON EL TEXTO. La posición es un cargo de recursos humanos, y AuthManager lo escribe tal
-- cual: en femenino. Si el puesto lo ocupa un hombre, el claim dirá "Encargado de Soporte
-- Técnico" —otra clave distinta para esta tabla— y esa vía dejará de funcionar. El rol
-- SuperAdministrador seguiría cubriéndolo, que es justo por qué conviene tener las dos.
--
-- Idempotente: se puede volver a ejecutar sin efectos secundarios.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

-- La tilde va con NCHAR(233) y no como carácter literal, para que la codificación con la que se
-- abra o ejecute este archivo no guarde una clave corrupta que luego no empareje con el token.
DECLARE @Posicion NVARCHAR(150) = N'Encargada de Soporte T' + NCHAR(233) + N'cnico';

-- 1) El alcance: Global.
MERGE dbo.PerfilesPorPosicion AS destino
USING (SELECT @Posicion AS Posicion) AS origen ON destino.Posicion = origen.Posicion
WHEN MATCHED AND destino.Perfil <> 1 THEN UPDATE SET Perfil = 1
WHEN NOT MATCHED THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, 1);

-- 2) Los permisos. Los mismos once de los otros perfiles Global. Sin filas aquí la comprobación
--    del script base aborta: una posición presente en una tabla y ausente en la otra es el estado
--    que no falla al insertarlo, sino el día que alguien de ese cargo entra.
MERGE dbo.PermisosPorPosicion AS destino
USING (
    SELECT @Posicion AS Posicion, q.Permiso
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

-- Esperado: Global con 11 permisos, y CodigoCaracter23 = 233 (la é de "Técnico"). Si sale otro
-- número, la tilde entró corrupta y esa clave no emparejará nunca con el token.
SELECT pf.Posicion,
       CASE pf.Perfil WHEN 1 THEN 'Global' WHEN 2 THEN 'Transportacion'
                      WHEN 3 THEN 'Filial' WHEN 4 THEN 'Tecnologia' END AS Perfil,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion p WHERE p.Posicion = pf.Posicion) AS Permisos,
       UNICODE(SUBSTRING(pf.Posicion, 23, 1)) AS CodigoCaracter23
FROM dbo.PerfilesPorPosicion pf
WHERE pf.Posicion LIKE N'Encargada de Soporte T%';
GO

-- Esperado: 9 y 71.
SELECT (SELECT COUNT(*) FROM dbo.PerfilesPorPosicion) AS Perfiles,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion) AS Permisos;
GO

-- Esperado: ninguna fila.
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
