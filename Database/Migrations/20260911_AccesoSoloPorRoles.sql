-- El acceso pasa a concederse SOLO por el claim "roles". La posición deja de otorgar.
--
-- Por qué: la posición es el cargo de recursos humanos. No se administra desde AuthManager, así
-- que no se puede revocar. Al sacar a alguien del grupo de super administradores conservaba el
-- alcance, porque su cargo se lo seguía dando por una vía que ese grupo nunca controló. Quitar un
-- acceso tiene que ser una sola acción y en un solo sitio; si no, se revoca creyendo que se
-- revocó, que es peor que no revocar.
--
-- ESTE SCRIPT NO CAMBIA EL ESQUEMA. Las tablas, columnas y CHECK quedan exactamente igual. Lo
-- único que cambia es CON QUÉ TEXTO las cruza el código, y eso vive en AuthManager, no aquí.
--
-- ------------------------------------------------------------------------------------------
-- QUÉ HAY QUE HACER ANTES
-- ------------------------------------------------------------------------------------------
-- Crear en AuthManager un grupo de seguridad por cada perfil y asignarle la gente. Hoy en
-- ADRTrack hay nueve filas y solo TRES están confirmadas como rol (se vieron en tokens reales):
--
--     SuperAdministrador                   Global          <- rol confirmado
--     Soporte Técnico                      Tecnología      <- rol confirmado
--     Encargado Transportación             Transportación  <- rol confirmado
--
--     Programador Senior                   Global          <- SIN CONFIRMAR
--     Administrador de Filial              Filial          <- SIN CONFIRMAR  (34 filiales)
--     Asistente Administrativo             Filial          <- SIN CONFIRMAR  (34 filiales)
--     Soporte Tecnico (sin tilde)          Tecnología      <- SIN CONFIRMAR
--
--     Encargada de Soporte Técnico         Global          <- es POSICIÓN: queda inerte
--     Encargado transportacion y mecanica  Transportación  <- es POSICIÓN: queda inerte
--
-- Las cuatro "SIN CONFIRMAR" son el riesgo real del despliegue. Si resultan ser posiciones y no
-- roles, esa gente entra autenticada, cae al respaldo Filial y TODAS las pantallas responden 403,
-- porque sin rol mapeado tampoco se le acumula ningún permiso. Las dos de Filial son las que más
-- duelen: son las 34 filiales.
--
-- Cómo saber cuál es cuál sin adivinar: iniciar sesión con un usuario de cada tipo y mirar
-- GET /api/perfil, que ahora devuelve "posicion" y "roles" por separado. Lo que salga en "roles"
-- es lo que hay que sembrar aquí, copiado carácter por carácter.
--
-- ------------------------------------------------------------------------------------------
-- QUÉ HACE ESTE SCRIPT
-- ------------------------------------------------------------------------------------------
-- Registra un rol nuevo copiando el perfil y los permisos de una clave que YA funciona, en vez de
-- volver a escribir la lista de permisos a mano. Escribirla a mano es justo donde se cuelan los
-- errores: un permiso mal tecleado no da error, simplemente esa pantalla responde 403 y nadie
-- sabe por qué.
--
-- Rellene la tabla @Equivalencias de abajo con los roles que haya creado en AuthManager. Mientras
-- esté vacía el script no toca nada: se puede ejecutar para ver solo el diagnóstico del final.
--
-- NO BORRA NADA. Las filas que son posiciones se quedan donde están, inertes. Borrarlas es una
-- limpieza aparte, y solo después de confirmar que todo el mundo entra bien por su rol.
--
-- Idempotente: se puede volver a ejecutar sin efectos secundarios.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

DECLARE @Equivalencias TABLE (
    RolNuevo   NVARCHAR(150) NOT NULL PRIMARY KEY,  -- el nombre EXACTO del grupo en AuthManager
    ClaveBase  NVARCHAR(150) NOT NULL               -- la fila que ya existe y de la que se copia
);

-- ------------------------------------------------------------------------------------------
-- RELLENAR AQUÍ. Los ejemplos están comentados: descoméntelos y ponga los nombres reales.
--
-- El nombre va tal como lo emite AuthManager, con sus tildes y sus mayúsculas. La base es
-- Modern_Spanish_CI_AS: ignora mayúsculas pero DISTINGUE TILDES, así que 'Soporte Tecnico' y
-- 'Soporte Técnico' son dos claves distintas.
-- ------------------------------------------------------------------------------------------
-- INSERT INTO @Equivalencias (RolNuevo, ClaveBase) VALUES
--     (N'LogiTrack.Filial',         N'Administrador de Filial'),
--     (N'LogiTrack.Tecnologia',     N'Soporte Técnico'),
--     (N'LogiTrack.Transportacion', N'Encargado Transportación'),
--     (N'LogiTrack.Global',         N'SuperAdministrador');

/* ---------------------------------------------------------------------------
   A partir de aquí no hay nada que tocar.
   --------------------------------------------------------------------------- */

-- Un nombre con espacio al principio o al final no cruza nunca, y a simple vista es idéntico al
-- correcto. Se corta de raíz antes de insertar nada.
UPDATE @Equivalencias SET RolNuevo = LTRIM(RTRIM(RolNuevo)), ClaveBase = LTRIM(RTRIM(ClaveBase));

-- Copiar de una clave que no existe dejaría el rol con perfil y sin permisos: entra, y todo
-- responde 403. Mejor fallar aquí, en voz alta.
IF EXISTS (SELECT 1 FROM @Equivalencias e
           WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilesPorPosicion p WHERE p.Posicion = e.ClaveBase))
BEGIN
    SELECT N'ClaveBase que no existe en PerfilesPorPosicion' AS Problema, e.RolNuevo, e.ClaveBase
    FROM @Equivalencias e
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilesPorPosicion p WHERE p.Posicion = e.ClaveBase);

    RAISERROR(N'Hay equivalencias que apuntan a una clave inexistente. Corrija la columna ClaveBase y vuelva a ejecutar. No se insertó nada.', 16, 1);
    RETURN;
END

-- 1) El alcance: SOBRE CUÁLES envíos.
MERGE dbo.PerfilesPorPosicion AS destino
USING (SELECT e.RolNuevo AS Posicion, p.Perfil
       FROM @Equivalencias e
       JOIN dbo.PerfilesPorPosicion p ON p.Posicion = e.ClaveBase) AS origen
    ON destino.Posicion = origen.Posicion
WHEN MATCHED AND destino.Perfil <> origen.Perfil THEN UPDATE SET Perfil = origen.Perfil
WHEN NOT MATCHED THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, origen.Perfil);

-- 2) Los permisos: QUÉ puede hacer. Sin estas filas el usuario entra con el alcance correcto y
--    todas las pantallas responden 403.
MERGE dbo.PermisosPorPosicion AS destino
USING (SELECT DISTINCT e.RolNuevo AS Posicion, p.Permiso
       FROM @Equivalencias e
       JOIN dbo.PermisosPorPosicion p ON p.Posicion = e.ClaveBase) AS origen
    ON destino.Posicion = origen.Posicion AND destino.Permiso = origen.Permiso
WHEN NOT MATCHED THEN INSERT (Posicion, Permiso) VALUES (origen.Posicion, origen.Permiso);
GO

/* ------------------------------ Diagnóstico ------------------------------ */

-- El estado completo de las dos tablas. Cada fila con 0 permisos es alguien que entrará sin poder
-- hacer nada; cada fila que no sea un rol de AuthManager es una fila que ya no concede nada.
SELECT pf.Posicion AS Clave,
       CASE pf.Perfil WHEN 1 THEN N'Global' WHEN 2 THEN N'Transportacion'
                      WHEN 3 THEN N'Filial' WHEN 4 THEN N'Tecnologia' END AS Perfil,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion p WHERE p.Posicion = pf.Posicion) AS Permisos
FROM dbo.PerfilesPorPosicion pf
ORDER BY pf.Perfil, pf.Posicion;
GO

-- Un perfil del CHECK sin ninguna clave que lo conceda es un módulo al que ya no llega nadie.
SELECT N'Perfil admitido por el CHECK y sin ninguna clave' AS Problema,
       N'Perfil ' + CAST(p.Perfil AS NVARCHAR(3)) AS Detalle
FROM (VALUES (1), (2), (3), (4)) AS p(Perfil)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilesPorPosicion x WHERE x.Perfil = p.Perfil);
GO

-- Perfil sin permisos: entra, y todo responde 403.
SELECT N'Clave con perfil pero sin permisos' AS Problema, pf.Posicion AS Detalle
FROM dbo.PerfilesPorPosicion pf
WHERE NOT EXISTS (SELECT 1 FROM dbo.PermisosPorPosicion p WHERE p.Posicion = pf.Posicion);
GO

-- Espacios sobrantes: invisibles al ojo, mortales para el cruce. <> no los detecta, DATALENGTH sí.
SELECT N'Clave con espacios sobrantes (perfiles)' AS Problema, N'[' + Posicion + N']' AS Detalle
FROM dbo.PerfilesPorPosicion
WHERE DATALENGTH(Posicion) <> DATALENGTH(LTRIM(RTRIM(Posicion)))
UNION ALL
SELECT N'Clave con espacios sobrantes (permisos)', N'[' + Posicion + N']'
FROM dbo.PermisosPorPosicion
WHERE DATALENGTH(Posicion) <> DATALENGTH(LTRIM(RTRIM(Posicion)));
GO
