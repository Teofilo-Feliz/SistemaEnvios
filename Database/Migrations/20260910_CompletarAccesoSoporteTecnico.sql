-- Completa el acceso de soporte técnico. Repara una aplicación a medias de 20260907.
--
-- El acceso de una posición vive en DOS tablas y hacen cosas distintas:
--
--   * PermisosPorPosicion  -> QUÉ puede hacer (la lee PermisosPorPosicionTransformation).
--   * PerfilesPorPosicion  -> SOBRE CUÁLES envíos (la lee AlcanceEnvios.ResolverPerfilAsync).
--
-- En dbqa se migró solo la primera. El resultado es el peor de los dos mundos y no se parece
-- a un fallo de configuración: el usuario entra, el menú se ve bien y los botones responden
-- —tiene sus permisos—, pero ResolverPerfilAsync no encuentra su posición y cae al respaldo
-- silencioso "tiene affiliate, luego es Filial" (AlcanceEnvios.cs). Como el affiliate de la
-- sede es el 30, y el 30 NO está mapeado a ninguna ubicación a propósito (ver
-- Ubicaciones_MapeoAuthManager.sql: si Tecnología llevara el 30, cualquier empleado de la sede
-- con perfil de filial vería todos los envíos), el tablero muere con "La filial 30 de su
-- usuario no está asociada a ninguna ubicación".
--
-- Ese mensaje casi nunca significa lo que dice. Significa que a PerfilesPorPosicion le falta
-- la fila de esa posición.
--
-- Es idempotente y cubre LAS DOS tablas, no solo la que falta hoy: así deja la base en el
-- estado correcto se ejecute desde donde se ejecute, y volver a correrlo no hace nada.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

-- Las dos grafías que emite AuthManager. La tilde se escribe con NCHAR(233) y no como carácter
-- literal para que la codificación con la que se abra o ejecute este archivo no guarde un
-- carácter corrupto que luego no empareje con el token.
--
-- Se registran LAS DOS a propósito: la base es Modern_Spanish_CI_AS, que ignora mayúsculas pero
-- DISTINGUE acentos, así que para SQL Server son dos claves diferentes. Con una sola fila, la
-- mitad del equipo entra por el módulo equivocado y el síntoma es "a unos les funciona y a
-- otros no", de los más difíciles de perseguir.
DECLARE @ConTilde NVARCHAR(150) = N'Soporte T' + NCHAR(233) + N'cnico';
DECLARE @SinTilde NVARCHAR(150) = N'Soporte Tecnico';

DECLARE @Claves TABLE (Posicion NVARCHAR(150) PRIMARY KEY);
INSERT INTO @Claves (Posicion) VALUES (@ConTilde), (@SinTilde);

-- Guarda: el perfil 4 nació con 20260907. Sin esto el MERGE fallaría con un error de CHECK que
-- no dice cuál es el problema real.
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_PerfilPosicion_Perfil'
      AND definition LIKE N'%4%')
BEGIN
    RAISERROR(N'CK_PerfilPosicion_Perfil no admite el perfil 4. Ejecute antes 20260907_PerfilTecnologia.sql.', 16, 1);
    RETURN;
END

-- 1) EL ALCANCE. Esta es la fila que falta en dbqa y la causa del error de la filial 30.
--    Perfil 4 = Tecnología: mismo alcance de DATOS que Global —Tecnología está en un extremo de
--    todo envío— pero acotado a su módulo, que lo deciden los permisos y el mapa del frontend.
MERGE dbo.PerfilesPorPosicion AS destino
USING (SELECT Posicion FROM @Claves) AS origen
    ON destino.Posicion = origen.Posicion
WHEN MATCHED AND destino.Perfil <> 4 THEN UPDATE SET Perfil = 4
WHEN NOT MATCHED THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, 4);

-- 2) LOS PERMISOS. En dbqa ya están y esto queda en no-op; se incluye para que el script deje
--    la base correcta también sobre una que solo tenga el seed base.
--
--    Quedan fuera a propósito 'catalogos.administrar' y 'transportes.administrar': administrar
--    el sistema y la flota no es su trabajo, y es lo único que lo separa de Programador Senior.
MERGE dbo.PermisosPorPosicion AS destino
USING (
    SELECT c.Posicion, q.Permiso
    FROM @Claves AS c
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

/* ------------------------------ Verificación ------------------------------ */

-- Esperado: dos filas con Perfil = 4, y CodigoCaracter10 debe salir 101 ('e') y 233 ('é'). Si
-- sale otro número, la tilde entró corrupta y esa clave no emparejará nunca con el token.
SELECT Posicion, Perfil, UNICODE(SUBSTRING(Posicion, 10, 1)) AS CodigoCaracter10
FROM dbo.PerfilesPorPosicion
WHERE Perfil = 4;
GO

-- Esperado: 7 y 49. Es el mismo conteo de una base sana.
SELECT (SELECT COUNT(*) FROM dbo.PerfilesPorPosicion) AS Perfiles,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion) AS Permisos;
GO

-- Esperado: ninguna fila. Una posición presente en una tabla y ausente en la otra es
-- exactamente el estado que causó este incidente, y no se nota hasta que alguien entra:
--
--   * permisos sin perfil -> entra, navega, y su alcance cae al respaldo Filial.
--   * perfil sin permisos -> entra al módulo correcto y todas las pantallas responden 403.
SELECT N'permisos sin perfil' AS Problema, pp.Posicion
FROM (SELECT DISTINCT Posicion FROM dbo.PermisosPorPosicion) AS pp
LEFT JOIN dbo.PerfilesPorPosicion AS pf ON pf.Posicion = pp.Posicion
WHERE pf.Posicion IS NULL
UNION ALL
SELECT N'perfil sin permisos', pf.Posicion
FROM dbo.PerfilesPorPosicion AS pf
LEFT JOIN dbo.PermisosPorPosicion AS pp ON pf.Posicion = pp.Posicion
WHERE pp.Posicion IS NULL;
GO
