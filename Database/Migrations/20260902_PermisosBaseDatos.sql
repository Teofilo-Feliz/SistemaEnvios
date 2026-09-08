-- Endurecimiento de permisos de base de datos (hallazgo S-3).
--
-- Al mover la autoridad de permisos desde el token hacia las tablas PerfilesPorPosicion y
-- PermisosPorPosicion, quien pueda escribir en ellas se otorga a sí mismo alcance Global. El
-- código no puede impedirlo: la mitigación es que el usuario con el que corre el API no tenga
-- permiso de escritura sobre esas dos tablas. Las mantiene Tecnología por fuera.
--
-- Ajuste el nombre del usuario antes de ejecutar.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @UsuarioApi SYSNAME = N'sistema_envios_app';   -- <-- usuario de la cadena de conexión
DECLARE @sql NVARCHAR(MAX);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @UsuarioApi)
BEGIN
    RAISERROR(N'El usuario %s no existe en esta base de datos. Corrija @UsuarioApi.', 16, 1, @UsuarioApi);
    RETURN;
END

-- Lectura sí: el API resuelve el perfil y los permisos en cada petición.
SET @sql = N'GRANT SELECT ON dbo.PerfilesPorPosicion TO ' + QUOTENAME(@UsuarioApi) + N';
             GRANT SELECT ON dbo.PermisosPorPosicion TO ' + QUOTENAME(@UsuarioApi) + N';';
EXEC sp_executesql @sql;

-- Escritura no: es la vía directa a concederse alcance Global.
SET @sql = N'DENY INSERT, UPDATE, DELETE ON dbo.PerfilesPorPosicion TO ' + QUOTENAME(@UsuarioApi) + N';
             DENY INSERT, UPDATE, DELETE ON dbo.PermisosPorPosicion TO ' + QUOTENAME(@UsuarioApi) + N';';
EXEC sp_executesql @sql;

-- El mapeo filial -> ubicación es la otra llave del alcance. El API crea ubicaciones
-- (UbicacionService) pero nunca escribe FilialExternaId, así que se deniega solo esa columna:
-- el mantenimiento normal sigue funcionando y el mapeo queda fuera de su alcance.
SET @sql = N'DENY UPDATE ON dbo.Ubicaciones(FilialExternaId) TO ' + QUOTENAME(@UsuarioApi) + N';';
EXEC sp_executesql @sql;

PRINT N'Permisos aplicados sobre las tablas de autoridad.';
GO
