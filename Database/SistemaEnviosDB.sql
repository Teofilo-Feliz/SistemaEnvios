/*
    Sistema de Envíos REH
    Esquema oficial alineado con el modelo EF Core.
    SQL Server 2019 o superior.

    Este script recrea por completo la base de datos. No debe ejecutarse
    sobre una base con información que deba conservarse.
*/

USE master;
GO

IF DB_ID(N'SistemaEnviosDB') IS NOT NULL
BEGIN
    ALTER DATABASE SistemaEnviosDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE SistemaEnviosDB;
END;
GO

CREATE DATABASE SistemaEnviosDB;
GO

USE SistemaEnviosDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.Ubicaciones
(
    UbicacionId INT IDENTITY(1,1) NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    CodigoCentro NVARCHAR(50) NOT NULL,
    -- Id de la filial en AuthManager (claim "affiliate"). NULL en ubicaciones que no son filial.
    FilialExternaId INT NULL,
    Tipo TINYINT NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Ubicaciones_Activo DEFAULT (1),
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Ubicaciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_Ubicaciones PRIMARY KEY (UbicacionId),
    CONSTRAINT UQ_Ubicaciones_CodigoCentro UNIQUE (CodigoCentro),
    CONSTRAINT CK_Ubicacion_Tipo CHECK (Tipo IN (1, 2))
);
GO

CREATE UNIQUE INDEX UX_Ubicaciones_FilialExternaId
    ON dbo.Ubicaciones(FilialExternaId)
    WHERE FilialExternaId IS NOT NULL;
GO

-- Acceso por posicion de AuthManager. Ver Database/Migrations/20260902_AccesoPorPosicion.sql
CREATE TABLE dbo.PerfilesPorPosicion
(
    Posicion NVARCHAR(150) NOT NULL,
    Perfil   TINYINT       NOT NULL,
    CONSTRAINT PK_PerfilesPorPosicion PRIMARY KEY (Posicion),
    CONSTRAINT CK_PerfilPosicion_Perfil CHECK (Perfil IN (1, 2, 3))
);
GO

CREATE TABLE dbo.PermisosPorPosicion
(
    Posicion NVARCHAR(150) NOT NULL,
    Permiso  NVARCHAR(50)  NOT NULL,
    CONSTRAINT PK_PermisosPorPosicion PRIMARY KEY (Posicion, Permiso)
);
GO

CREATE TABLE dbo.TiposEquipo
(
    TipoEquipoId INT IDENTITY(1,1) NOT NULL,
    Nombre NVARCHAR(100) NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_TiposEquipo_Activo DEFAULT (1),
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_TiposEquipo_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_TiposEquipo PRIMARY KEY (TipoEquipoId),
    CONSTRAINT UQ_TiposEquipo_Nombre UNIQUE (Nombre)
);
GO

CREATE TABLE dbo.TiposTransporte
(
    TipoTransporteId INT IDENTITY(1,1) NOT NULL,
    Codigo NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(100) NOT NULL,
    Estrategia TINYINT NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_TiposTransporte_Activo DEFAULT (1),
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_TiposTransporte_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TiposTransporte PRIMARY KEY (TipoTransporteId),
    CONSTRAINT UQ_TiposTransporte_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_TiposTransporte_Estrategia CHECK (Estrategia IN (1,2))
);
GO

CREATE TABLE dbo.ChoferesInternos
(
    ChoferInternoId INT IDENTITY(1,1) NOT NULL,
    NombreCompleto NVARCHAR(150) NOT NULL,
    NumeroEmpleado NVARCHAR(30) NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_ChoferesInternos_Activo DEFAULT (1),
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_ChoferesInternos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_ChoferesInternos PRIMARY KEY (ChoferInternoId),
    CONSTRAINT UQ_ChoferesInternos_NumeroEmpleado UNIQUE (NumeroEmpleado)
);
GO

CREATE TABLE dbo.UsuariosReferencia
(
    UsuarioExternoId UNIQUEIDENTIFIER NOT NULL,
    NombreCompleto NVARCHAR(150) NOT NULL,
    NumeroEmpleado NVARCHAR(30) NULL,
    Correo NVARCHAR(254) NULL,
    EsTecnico BIT NOT NULL,
    Activo BIT NOT NULL,
    FechaUltimaSincronizacion DATETIME2(7) NOT NULL,
    Origen NVARCHAR(30) NOT NULL CONSTRAINT DF_UsuariosReferencia_Origen DEFAULT(N'AUTHMANAGER'),
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_UsuariosReferencia PRIMARY KEY(UsuarioExternoId)
);
GO
CREATE UNIQUE INDEX UX_UsuariosReferencia_NumeroEmpleado ON dbo.UsuariosReferencia(NumeroEmpleado) WHERE NumeroEmpleado IS NOT NULL;
CREATE INDEX IX_UsuariosReferencia_TecnicosActivos ON dbo.UsuariosReferencia(EsTecnico,Activo,NombreCompleto);
GO

CREATE TABLE dbo.EstadosEnvio
(
    EstadoEnvioId INT IDENTITY(1,1) NOT NULL,
    Codigo NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(100) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    EsFinal BIT NOT NULL CONSTRAINT DF_EstadosEnvio_EsFinal DEFAULT (0),
    Activo BIT NOT NULL CONSTRAINT DF_EstadosEnvio_Activo DEFAULT (1),
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_EstadosEnvio_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_EstadosEnvio PRIMARY KEY (EstadoEnvioId),
    CONSTRAINT UQ_EstadosEnvio_Codigo UNIQUE (Codigo)
);
GO

CREATE TABLE dbo.Equipos
(
    EquipoId INT IDENTITY(1,1) NOT NULL,
    CodigoActivo NVARCHAR(50) NULL,
    NumeroSerie NVARCHAR(100) NULL,
    TipoEquipoId INT NOT NULL,
    UbicacionActualId INT NOT NULL,
    Marca NVARCHAR(100) NOT NULL,
    Modelo NVARCHAR(100) NOT NULL,
    Observaciones NVARCHAR(2000) NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Equipos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Equipos PRIMARY KEY (EquipoId),
    CONSTRAINT FK_Equipos_TiposEquipo FOREIGN KEY (TipoEquipoId) REFERENCES dbo.TiposEquipo(TipoEquipoId),
    CONSTRAINT FK_Equipos_Ubicaciones FOREIGN KEY (UbicacionActualId) REFERENCES dbo.Ubicaciones(UbicacionId)
);
GO

CREATE UNIQUE INDEX UX_Equipos_CodigoActivo ON dbo.Equipos(CodigoActivo) WHERE CodigoActivo IS NOT NULL;
CREATE UNIQUE INDEX UX_Equipos_NumeroSerie ON dbo.Equipos(NumeroSerie) WHERE NumeroSerie IS NOT NULL;
CREATE INDEX IX_Equipos_TipoEquipoId ON dbo.Equipos(TipoEquipoId);
CREATE INDEX IX_Equipos_UbicacionActualId ON dbo.Equipos(UbicacionActualId);
GO

CREATE TABLE dbo.Envios
(
    EnvioId INT IDENTITY(1,1) NOT NULL,
    NumeroEnvio NVARCHAR(30) NOT NULL,
    UbicacionOrigenId INT NOT NULL,
    UbicacionDestinoId INT NOT NULL,
    EstadoEnvioId INT NOT NULL,
    Direccion TINYINT NOT NULL,
    UsuarioSolicitanteId UNIQUEIDENTIFIER NOT NULL,
    FechaFinalizacion DATETIME2(7) NULL,
    Observaciones NVARCHAR(2000) NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Envios_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Envios PRIMARY KEY (EnvioId),
    CONSTRAINT UQ_Envios_NumeroEnvio UNIQUE (NumeroEnvio),
    CONSTRAINT FK_Envios_UbicacionOrigen FOREIGN KEY (UbicacionOrigenId) REFERENCES dbo.Ubicaciones(UbicacionId),
    CONSTRAINT FK_Envios_UbicacionDestino FOREIGN KEY (UbicacionDestinoId) REFERENCES dbo.Ubicaciones(UbicacionId),
    CONSTRAINT FK_Envios_EstadosEnvio FOREIGN KEY (EstadoEnvioId) REFERENCES dbo.EstadosEnvio(EstadoEnvioId),
    CONSTRAINT CK_Envio_UbicacionesDistintas CHECK (UbicacionOrigenId <> UbicacionDestinoId),
    CONSTRAINT CK_Envio_Direccion CHECK (Direccion IN (1, 2))
);
GO

CREATE INDEX IX_Envios_EstadoEnvioId ON dbo.Envios(EstadoEnvioId);
CREATE INDEX IX_Envios_UbicacionOrigenId ON dbo.Envios(UbicacionOrigenId);
CREATE INDEX IX_Envios_UbicacionDestinoId ON dbo.Envios(UbicacionDestinoId);
CREATE INDEX IX_Envios_FechaCreacion ON dbo.Envios(FechaCreacion DESC);
CREATE INDEX IX_Envios_Estado_Fecha ON dbo.Envios(EstadoEnvioId, FechaCreacion DESC);
GO

CREATE TABLE dbo.ReservasEquipoEnvio
(
    EquipoId INT NOT NULL,
    EnvioId INT NOT NULL,
    FechaReserva DATETIME2(7) NOT NULL,
    UsuarioId UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_ReservasEquipoEnvio PRIMARY KEY (EquipoId),
    CONSTRAINT FK_ReservasEquipoEnvio_Equipos FOREIGN KEY (EquipoId) REFERENCES dbo.Equipos(EquipoId),
    CONSTRAINT FK_ReservasEquipoEnvio_Envios FOREIGN KEY (EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE
);
GO

CREATE INDEX IX_ReservasEquipoEnvio_EnvioId ON dbo.ReservasEquipoEnvio(EnvioId);
GO

CREATE TABLE dbo.TransicionesEstadoEnvio
(
    TransicionEstadoEnvioId INT IDENTITY(1,1) NOT NULL,
    EstadoOrigenId INT NOT NULL,
    EstadoDestinoId INT NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_TransicionesEstadoEnvio_Activo DEFAULT (1),
    CONSTRAINT PK_TransicionesEstadoEnvio PRIMARY KEY (TransicionEstadoEnvioId),
    CONSTRAINT UQ_TransicionesEstadoEnvio UNIQUE (EstadoOrigenId, EstadoDestinoId),
    CONSTRAINT FK_Transiciones_EstadoOrigen FOREIGN KEY (EstadoOrigenId) REFERENCES dbo.EstadosEnvio(EstadoEnvioId),
    CONSTRAINT FK_Transiciones_EstadoDestino FOREIGN KEY (EstadoDestinoId) REFERENCES dbo.EstadosEnvio(EstadoEnvioId),
    CONSTRAINT CK_Transiciones_EstadosDistintos CHECK (EstadoOrigenId <> EstadoDestinoId)
);
GO

CREATE TABLE dbo.EnvioEquipos
(
    EnvioEquipoId INT IDENTITY(1,1) NOT NULL,
    EnvioId INT NOT NULL,
    EquipoId INT NOT NULL,
    NumeroTicket NVARCHAR(50) NOT NULL,
    UsuarioSolicitanteId UNIQUEIDENTIFIER NOT NULL,
    Observaciones NVARCHAR(2000) NOT NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_EnvioEquipos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_EnvioEquipos PRIMARY KEY (EnvioEquipoId),
    CONSTRAINT UQ_EnvioEquipos_EnvioEquipo UNIQUE (EnvioId, EquipoId),
    CONSTRAINT FK_EnvioEquipos_Envios FOREIGN KEY (EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE,
    CONSTRAINT FK_EnvioEquipos_Equipos FOREIGN KEY (EquipoId) REFERENCES dbo.Equipos(EquipoId)
);
GO

CREATE INDEX IX_EnvioEquipos_EquipoId ON dbo.EnvioEquipos(EquipoId);
CREATE UNIQUE INDEX UX_EnvioEquipos_NumeroTicket ON dbo.EnvioEquipos(NumeroTicket);
GO

CREATE TABLE dbo.Transportes
(
    TransporteId INT IDENTITY(1,1) NOT NULL,
    EnvioId INT NOT NULL,
    TipoTransporteId INT NOT NULL,
    Observaciones NVARCHAR(2000) NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Transportes_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Transportes PRIMARY KEY (TransporteId),
    CONSTRAINT UQ_Transportes_EnvioId UNIQUE (EnvioId),
    CONSTRAINT FK_Transportes_Envios FOREIGN KEY (EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE,
    CONSTRAINT FK_Transportes_TiposTransporte FOREIGN KEY (TipoTransporteId) REFERENCES dbo.TiposTransporte(TipoTransporteId)
);
GO

CREATE INDEX IX_Transportes_TipoTransporteId ON dbo.Transportes(TipoTransporteId);
GO

CREATE TABLE dbo.TransportesInternos
(
    TransporteId INT NOT NULL,
    ChoferInternoId INT NOT NULL,
    NombreChoferAlMomento NVARCHAR(150) NOT NULL,
    NumeroEmpleadoAlMomento NVARCHAR(30) NOT NULL,
    FechaEntregaTransportacion DATETIME2(7) NULL,
    EntregaConfirmada BIT NOT NULL CONSTRAINT DF_TransportesInternos_Confirmada DEFAULT (0),
    FechaConfirmacionEntrega DATETIME2(7) NULL,
    UsuarioConfirmacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TransportesInternos PRIMARY KEY (TransporteId),
    CONSTRAINT FK_TransportesInternos_Transportes FOREIGN KEY (TransporteId) REFERENCES dbo.Transportes(TransporteId) ON DELETE CASCADE,
    CONSTRAINT FK_TransportesInternos_Choferes FOREIGN KEY (ChoferInternoId) REFERENCES dbo.ChoferesInternos(ChoferInternoId),
    CONSTRAINT CK_TransportesInternos_Confirmacion CHECK ((EntregaConfirmada=0 AND FechaConfirmacionEntrega IS NULL AND UsuarioConfirmacionId IS NULL) OR (EntregaConfirmada=1 AND FechaEntregaTransportacion IS NOT NULL AND FechaConfirmacionEntrega>=FechaEntregaTransportacion AND UsuarioConfirmacionId IS NOT NULL))
);
GO
CREATE INDEX IX_TransportesInternos_ChoferInternoId ON dbo.TransportesInternos(ChoferInternoId);
GO

CREATE TABLE dbo.TransportesPrivados
(
    TransporteId INT NOT NULL,
    NombreResponsable NVARCHAR(150) NOT NULL,
    Parentesco NVARCHAR(50) NOT NULL,
    CedulaResponsable VARCHAR(11) NOT NULL,
    PlacaVehiculo NVARCHAR(20) NOT NULL,
    FechaEntrega DATETIME2(7) NULL,
    UsuarioQueEntregoId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TransportesPrivados PRIMARY KEY (TransporteId),
    CONSTRAINT FK_TransportesPrivados_Transportes FOREIGN KEY (TransporteId) REFERENCES dbo.Transportes(TransporteId) ON DELETE CASCADE,
    CONSTRAINT CK_TransportesPrivados_Cedula CHECK (CedulaResponsable NOT LIKE '%[^0-9]%' AND LEN(CedulaResponsable)=11)
);
GO

CREATE TABLE dbo.Recepciones
(
    RecepcionId INT IDENTITY(1,1) NOT NULL,
    EnvioId INT NOT NULL,
    TecnicoAsignadoUsuarioId UNIQUEIDENTIFIER NULL,
    TecnicoAsignadoNombre NVARCHAR(150) NULL,
    TecnicoAsignadoNumeroEmpleado NVARCHAR(30) NULL,
    UsuarioQueRecibioId UNIQUEIDENTIFIER NULL,
    FechaAsignacion DATETIME2(7) NULL,
    FechaRecepcion DATETIME2(7) NULL,
    EstadoRecepcion INT NOT NULL,
    Observaciones NVARCHAR(2000) NULL,
    UsuarioQueAsignoId UNIQUEIDENTIFIER NOT NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Recepciones_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_Recepciones PRIMARY KEY (RecepcionId),
    CONSTRAINT UQ_Recepciones_EnvioId UNIQUE (EnvioId),
    CONSTRAINT FK_Recepciones_Envios FOREIGN KEY (EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE,
    CONSTRAINT FK_Recepciones_UsuariosReferencia FOREIGN KEY (TecnicoAsignadoUsuarioId) REFERENCES dbo.UsuariosReferencia(UsuarioExternoId),
    CONSTRAINT CK_Recepciones_Estado CHECK (EstadoRecepcion BETWEEN 1 AND 5)
);
GO

CREATE TABLE dbo.RecepcionEquipos
(
    RecepcionEquipoId INT IDENTITY(1,1) NOT NULL,
    RecepcionId INT NOT NULL,
    EnvioEquipoId INT NOT NULL,
    EstadoRecepcionEquipo INT NOT NULL,
    FechaVerificacion DATETIME2(7) NULL,
    Observaciones NVARCHAR(2000) NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_RecepcionEquipos_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_RecepcionEquipos PRIMARY KEY (RecepcionEquipoId),
    CONSTRAINT UQ_RecepcionEquipos_EnvioEquipoId UNIQUE (EnvioEquipoId),
    CONSTRAINT FK_RecepcionEquipos_Recepciones FOREIGN KEY (RecepcionId) REFERENCES dbo.Recepciones(RecepcionId) ON DELETE CASCADE,
    CONSTRAINT FK_RecepcionEquipos_EnvioEquipos FOREIGN KEY (EnvioEquipoId) REFERENCES dbo.EnvioEquipos(EnvioEquipoId),
    CONSTRAINT CK_RecepcionEquipos_Estado CHECK (EstadoRecepcionEquipo BETWEEN 1 AND 3),
    CONSTRAINT CK_RecepcionEquipos_Incidencia CHECK
    (
        EstadoRecepcionEquipo <> 3 OR NULLIF(LTRIM(RTRIM(Observaciones)), N'') IS NOT NULL
    )
);
GO

CREATE INDEX IX_RecepcionEquipos_RecepcionId ON dbo.RecepcionEquipos(RecepcionId);
GO

CREATE TABLE dbo.Incidencias
(
    IncidenciaId INT IDENTITY(1,1) NOT NULL,
    EnvioId INT NOT NULL,
    EnvioEquipoId INT NULL,
    TransporteId INT NULL,
    Descripcion NVARCHAR(2000) NOT NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Incidencias_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_Incidencias PRIMARY KEY (IncidenciaId),
    CONSTRAINT FK_Incidencias_Envios FOREIGN KEY (EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE,
    CONSTRAINT FK_Incidencias_EnvioEquipos FOREIGN KEY (EnvioEquipoId) REFERENCES dbo.EnvioEquipos(EnvioEquipoId),
    CONSTRAINT FK_Incidencias_Transportes FOREIGN KEY (TransporteId) REFERENCES dbo.Transportes(TransporteId),
    CONSTRAINT CK_Incidencias_UnSoloAlcance CHECK (NOT (EnvioEquipoId IS NOT NULL AND TransporteId IS NOT NULL))
);
GO

CREATE INDEX IX_Incidencias_EnvioId ON dbo.Incidencias(EnvioId);
CREATE INDEX IX_Incidencias_EnvioEquipoId ON dbo.Incidencias(EnvioEquipoId);
CREATE INDEX IX_Incidencias_TransporteId ON dbo.Incidencias(TransporteId);
GO

CREATE TABLE dbo.HistorialesEstadoEnvio
(
    HistorialEstadoEnvioId INT IDENTITY(1,1) NOT NULL,
    EnvioId INT NOT NULL,
    EstadoEnvioId INT NOT NULL,
    Fecha DATETIME2(7) NOT NULL,
    UbicacionId INT NOT NULL,
    UsuarioId UNIQUEIDENTIFIER NOT NULL,
    Observaciones NVARCHAR(2000) NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Historiales_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    UsuarioCreacionId UNIQUEIDENTIFIER NULL,
    FechaModificacion DATETIME2(7) NULL,
    UsuarioModificacionId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_HistorialesEstadoEnvio PRIMARY KEY (HistorialEstadoEnvioId),
    CONSTRAINT FK_Historiales_Envios FOREIGN KEY (EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE,
    CONSTRAINT FK_Historiales_EstadosEnvio FOREIGN KEY (EstadoEnvioId) REFERENCES dbo.EstadosEnvio(EstadoEnvioId),
    CONSTRAINT FK_Historiales_Ubicaciones FOREIGN KEY (UbicacionId) REFERENCES dbo.Ubicaciones(UbicacionId)
);
GO

CREATE INDEX IX_Historiales_Envio_Fecha ON dbo.HistorialesEstadoEnvio(EnvioId, Fecha);
CREATE INDEX IX_Historiales_EstadoEnvioId ON dbo.HistorialesEstadoEnvio(EstadoEnvioId);
CREATE INDEX IX_Historiales_UbicacionId ON dbo.HistorialesEstadoEnvio(UbicacionId);
GO

CREATE TABLE dbo.Notificaciones
(
    NotificacionId BIGINT IDENTITY(1,1) NOT NULL,
    EnvioId INT NOT NULL,
    Tipo NVARCHAR(50) NOT NULL,
    Titulo NVARCHAR(200) NOT NULL,
    Mensaje NVARCHAR(1000) NOT NULL,
    DestinatarioRol NVARCHAR(50) NOT NULL,
    DestinatarioUsuarioId UNIQUEIDENTIFIER NULL,
    FechaCreacion DATETIME2(7) NOT NULL CONSTRAINT DF_Notificaciones_Fecha DEFAULT(SYSUTCDATETIME()),
    FechaLeida DATETIME2(7) NULL,
    UsuarioLecturaId UNIQUEIDENTIFIER NULL,
    CONSTRAINT PK_Notificaciones PRIMARY KEY(NotificacionId),
    CONSTRAINT FK_Notificaciones_Envios FOREIGN KEY(EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_Notificaciones_Rol_Leida_Fecha ON dbo.Notificaciones(DestinatarioRol,FechaLeida,FechaCreacion DESC);
GO

INSERT INTO dbo.EstadosEnvio (Codigo, Nombre, Descripcion, EsFinal, Activo)
VALUES
    (N'EN_FILIAL', N'En filial', N'El envío hacia Tecnología se prepara en la filial.', 0, 1),
    (N'ENTREGADO_TRANSPORTACION', N'Entregado a transportación', N'La filial entregó el envío a transportación.', 0, 1),
    (N'DESPACHADO_TRANSPORTE_PRIVADO', N'Entregado a transporte privado', N'La filial entregó el envío a un responsable privado.', 0, 1),
    (N'PENDIENTE_CONFIRMACION_TRANSPORTE', N'Pendiente de confirmación', N'Transportación debe confirmar la custodia.', 0, 1),
    (N'CONFIRMADO_TRANSPORTACION', N'Confirmado por transportación', N'Transportación confirmó la recepción del envío.', 0, 1),
    (N'EN_TRANSITO', N'En tránsito', N'El envío se encuentra en traslado.', 0, 1),
    (N'RECIBIDO_TRANSPORTACION', N'Recibido por transportación', N'El envío llegó al punto de recepción logística.', 0, 1),
    (N'ESPERA_TECNOLOGIA', N'En espera de Tecnología', N'El envío espera ser procesado por Tecnología.', 0, 1),
    (N'EN_REVISION', N'En proceso de revisión', N'Tecnología está verificando los equipos recibidos.', 0, 1),
    (N'RECIBIDO_TECNOLOGIA', N'Recibido por Tecnología', N'La recepción en Tecnología fue completada.', 1, 1),
    (N'INCIDENCIA_TRANSPORTACION', N'Incidencia en transportación', N'El traslado presenta una incidencia pendiente.', 0, 1),
    (N'PREPARACION_TECNOLOGIA', N'En preparación por Tecnología', N'Tecnología prepara un nuevo envío hacia una filial.', 0, 1),
    (N'DESPACHADO_TECNOLOGIA', N'Despachado por Tecnología', N'Tecnología entregó el envío para su traslado.', 0, 1),
    (N'EN_TRANSPORTACION', N'En Transportación', N'El envío fue recibido por el área de Transportación.', 0, 1),
    (N'TRANSPORTE_ASIGNADO', N'Chofer asignado', N'Transportación asignó el chofer al envío.', 0, 1),
    (N'DESPACHADO_TRANSPORTACION', N'Despachado por Transportación', N'Transportación despachó el envío hacia la filial.', 0, 1),
    (N'PENDIENTE_RECEPCION_FILIAL', N'Pendiente de recepción en filial', N'El envío espera recepción en la filial destino.', 0, 1),
    (N'RECIBIDO_FILIAL', N'Recibido en filial', N'La filial recibió físicamente el envío.', 0, 1),
    (N'RECEPCION_VALIDADA_FILIAL', N'Recepción validada en filial', N'La filial verificó y cerró la recepción.', 1, 1);
GO

INSERT INTO dbo.TransicionesEstadoEnvio (EstadoOrigenId, EstadoDestinoId, Activo)
SELECT origen.EstadoEnvioId, destino.EstadoEnvioId, 1
FROM
(
    VALUES
        (N'EN_FILIAL', N'ENTREGADO_TRANSPORTACION'),
        (N'ENTREGADO_TRANSPORTACION', N'PENDIENTE_CONFIRMACION_TRANSPORTE'),
        (N'EN_FILIAL', N'DESPACHADO_TRANSPORTE_PRIVADO'),
        (N'DESPACHADO_TRANSPORTE_PRIVADO', N'EN_TRANSITO'),
        (N'PENDIENTE_CONFIRMACION_TRANSPORTE', N'CONFIRMADO_TRANSPORTACION'),
        (N'CONFIRMADO_TRANSPORTACION', N'EN_TRANSITO'),
        (N'EN_TRANSITO', N'RECIBIDO_TRANSPORTACION'),
        (N'EN_TRANSITO', N'INCIDENCIA_TRANSPORTACION'),
        (N'INCIDENCIA_TRANSPORTACION', N'EN_TRANSITO'),
        (N'RECIBIDO_TRANSPORTACION', N'ESPERA_TECNOLOGIA'),
        (N'EN_TRANSITO', N'ESPERA_TECNOLOGIA'),
        (N'ESPERA_TECNOLOGIA', N'EN_REVISION'),
        (N'EN_REVISION', N'RECIBIDO_TECNOLOGIA'),
        (N'PREPARACION_TECNOLOGIA', N'DESPACHADO_TECNOLOGIA'),
        (N'DESPACHADO_TECNOLOGIA', N'EN_TRANSPORTACION'),
        (N'EN_TRANSPORTACION', N'TRANSPORTE_ASIGNADO'),
        (N'TRANSPORTE_ASIGNADO', N'DESPACHADO_TRANSPORTACION'),
        (N'DESPACHADO_TRANSPORTACION', N'EN_TRANSITO'),
        (N'EN_TRANSITO', N'PENDIENTE_RECEPCION_FILIAL'),
        (N'PENDIENTE_RECEPCION_FILIAL', N'RECIBIDO_FILIAL'),
        (N'RECIBIDO_FILIAL', N'RECEPCION_VALIDADA_FILIAL')
) AS flujo(CodigoOrigen, CodigoDestino)
INNER JOIN dbo.EstadosEnvio origen ON origen.Codigo = flujo.CodigoOrigen
INNER JOIN dbo.EstadosEnvio destino ON destino.Codigo = flujo.CodigoDestino;
GO

INSERT INTO dbo.TiposTransporte (Codigo, Nombre, Estrategia, Activo)
VALUES (N'INTERNO', N'Interno', 1, 1), (N'PRIVADO', N'Privado', 2, 1);
GO

-- Ubicaciones reales de ADR: 34 filiales + Tecnologia.
--
-- Tipo 2 = Tecnologia, ubicada fisicamente en la Sede Nacional (centro 30 de AuthManager).
-- Es el destino de todo envio que sale de una filial y el origen de todo envio de retorno.
--
-- FilialExternaId es el id de filial que AuthManager emite en el claim "affiliate"
-- ("30,SANTO DOMINGO (SEDE)"). Solo el 30 esta confirmado; el resto queda NULL a proposito:
-- un id inventado se veria correcto y acotaria mal los envios. Mientras este NULL, sus
-- usuarios reciben "La filial N no esta asociada a ninguna ubicacion" en vez de una lista
-- vacia silenciosa. Completar con Database/Ubicaciones_MapeoAuthManager.sql.
--
-- Tecnologia va sin id a proposito: quien trabaja en Tecnologia obtiene alcance Global por
-- su posicion. Darle el 30 (centro sede) haria que CUALQUIER empleado de la sede con perfil
-- de filial viera todos los envios, porque todos pasan por Tecnologia.
INSERT INTO dbo.Ubicaciones (Nombre, CodigoCentro, FilialExternaId, Tipo, Activo)
VALUES
    (N'Tecnología',                    N'TECNOLOGIA',               NULL, 2, 1),
    (N'Santo Domingo Oeste',           N'SANTO-DOMINGO-OESTE',         4, 1, 1),
    (N'Santo Domingo Este',            N'SANTO-DOMINGO-ESTE',         16, 1, 1),
    (N'Guerra',                        N'GUERRA',                     10, 1, 1),
    (N'San Cristóbal',                 N'SAN-CRISTOBAL',              21, 1, 1),
    (N'Haina',                         N'HAINA',                      37, 1, 1),
    (N'Baní',                          N'BANI',                        2, 1, 1),
    (N'Azua',                          N'AZUA',                        1, 1, 1),
    (N'San José de Ocoa',              N'SAN-JOSE-DE-OCOA',           23, 1, 1),
    (N'Rancho Arriba',                 N'RANCHO-ARRIBA',              36, 1, 1),
    (N'San Juan de la Maguana',        N'SAN-JUAN-DE-LA-MAGUANA',     24, 1, 1),
    (N'Las Matas de Farfán',           N'LAS-MATAS-DE-FARFAN',        34, 1, 1),
    (N'Barahona',                      N'BARAHONA',                    3, 1, 1),
    (N'Bonao',                         N'BONAO',                       5, 1, 1),
    (N'Maimón',                        N'MAIMON',                     39, 1, 1),
    (N'La Vega',                       N'LA-VEGA',                    15, 1, 1),
    (N'Constanza',                     N'CONSTANZA',                   6, 1, 1),
    (N'Jarabacoa',                     N'JARABACOA',                  13, 1, 1),
    (N'Santiago',                      N'SANTIAGO',                   27, 1, 1),
    (N'Puerto Plata',                  N'PUERTO-PLATA',               19, 1, 1),
    (N'Sosúa',                         N'SOSUA',                      28, 1, 1),
    (N'Montecristi',                   N'MONTECRISTI',                17, 1, 1),
    (N'Dajabón',                       N'DAJABON',                     8, 1, 1),
    (N'Cotuí',                         N'COTUI',                       7, 1, 1),
    (N'Mao',                           N'MAO',                        29, 1, 1),
    (N'Salcedo',                       N'SALCEDO',                    20, 1, 1),
    (N'Nagua',                         N'NAGUA',                      18, 1, 1),
    (N'Sánchez',                       N'SANCHEZ',                    26, 1, 1),
    (N'San Francisco de Macorís',      N'SAN-FRANCISCO-DE-MACORIS',   22, 1, 1),
    (N'Luperón',                       N'LUPERON',                    35, 1, 1),
    (N'Hato Mayor',                    N'HATO-MAYOR',                 11, 1, 1),
    (N'El Seibo',                      N'EL-SEIBO',                    9, 1, 1),
    (N'San Pedro de Macorís',          N'SAN-PEDRO-DE-MACORIS',       25, 1, 1),
    (N'La Romana',                     N'LA-ROMANA',                  14, 1, 1),
    (N'Higüey',                        N'HIGUEY',                     12, 1, 1);
GO
INSERT INTO dbo.TiposEquipo (Nombre, Activo)
VALUES (N'Laptop', 1), (N'Computadora de escritorio', 1), (N'Monitor', 1), (N'Impresora', 1), (N'Otro', 1);
GO
