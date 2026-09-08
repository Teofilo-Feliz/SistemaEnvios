-- Login y usuario con los que el API se conecta.
--
-- Hasta ahora esto no existía en ningún script: en desarrollo cada quien usaba su propio usuario
-- de SQL Server (el de la cadena de conexión local es una cuenta personal). Al dockerizar eso no
-- sirve —el contenedor arranca con una base limpia y solo 'sa'— y además 20260902 aborta con
-- "El usuario sistema_envios_app no existe en esta base de datos".
--
-- Se separa del esquema a propósito: el script de esquema hace DROP DATABASE, y el login vive
-- fuera de la base, en el servidor.
--
-- ANTES DE EJECUTAR: cambie la contraseña. Está aquí como marcador y este archivo se versiona.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE master;
GO

DECLARE @Usuario  SYSNAME       = N'sistema_envios_app';
DECLARE @Clave    NVARCHAR(128) = N'CAMBIE-ESTA-CLAVE';
DECLARE @BaseNom  SYSNAME       = N'ADRTrack';
DECLARE @sql      NVARCHAR(MAX);

IF @Clave = N'CAMBIE-ESTA-CLAVE'
BEGIN
    RAISERROR(N'Defina una contraseña real en @Clave antes de ejecutar este script.', 16, 1);
    RETURN;
END

-- El login es del servidor y sobrevive al DROP DATABASE del script de esquema.
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @Usuario)
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@Usuario) +
               N' WITH PASSWORD = ' + QUOTENAME(@Clave, '''') +
               N', CHECK_POLICY = ON, DEFAULT_DATABASE = ' + QUOTENAME(@BaseNom) + N';';
    EXEC sp_executesql @sql;
END
GO

USE ADRTrack;
GO

DECLARE @Usuario SYSNAME = N'sistema_envios_app';
DECLARE @sql NVARCHAR(MAX);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @Usuario)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@Usuario) + N' FOR LOGIN ' + QUOTENAME(@Usuario) + N';';
    EXEC sp_executesql @sql;
END

-- Lectura y escritura de datos, nada de esquema: el API no crea ni altera tablas. Los permisos
-- finos sobre las tablas de acceso los ajusta 20260902_PermisosBaseDatos.sql, que debe
-- ejecutarse DESPUÉS de este script.
SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@Usuario) + N';
             ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@Usuario) + N';';
EXEC sp_executesql @sql;
GO

SELECT name AS UsuarioCreado, type_desc
FROM sys.database_principals
WHERE name = N'sistema_envios_app';
GO
