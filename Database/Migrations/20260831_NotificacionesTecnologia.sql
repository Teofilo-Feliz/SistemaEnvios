USE SistemaEnviosDB;
GO
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.Notificaciones',N'U') IS NULL
BEGIN
CREATE TABLE dbo.Notificaciones
(
    NotificacionId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notificaciones PRIMARY KEY,
    EnvioId INT NOT NULL,
    Tipo NVARCHAR(50) NOT NULL,
    Titulo NVARCHAR(200) NOT NULL,
    Mensaje NVARCHAR(1000) NOT NULL,
    DestinatarioRol NVARCHAR(50) NOT NULL,
    DestinatarioUsuarioId UNIQUEIDENTIFIER NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Notificaciones_Fecha DEFAULT(SYSUTCDATETIME()),
    FechaLeida DATETIME2(7) NULL,
    UsuarioLecturaId UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_Notificaciones_Envios FOREIGN KEY(EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE
);
CREATE INDEX IX_Notificaciones_Rol_Leida_Fecha ON dbo.Notificaciones(DestinatarioRol,FechaLeida,FechaCreacion DESC);
END;
GO
