
-- Script base de datos ADRTrack. Contiene la estructura de tablas y relaciones, y los datos iniciales de referencia.
--
-- ATENCIÓN: este script RECREA la base por completo. Hace DROP DATABASE antes de crearla, así
-- que ejecutarlo sobre un entorno con datos los borra sin aviso. No lo use para actualizar una
-- base existente: para eso están los scripts de Database/Migrations.
USE master;
GO

IF DB_ID(N'ADRTrack') IS NOT NULL
BEGIN
    ALTER DATABASE ADRTrack SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ADRTrack;
END;
GO

-- La colación se fija explícitamente y no se hereda del servidor. En la máquina de desarrollo
-- el servidor es Modern_Spanish_CI_AS, pero una imagen de SQL Server para Linux arranca con
-- SQL_Latin1_General_CP1_CI_AS: la base saldría distinta según dónde se cree.
--
-- El "_AS" (accent sensitive) no es un detalle: PerfilesPorPosicion.Posicion es la clave
-- primaria, y AuthManager emite la misma posición con y sin tilde ('Soporte Técnico' y
-- 'Soporte Tecnico'). Con una colación acento-insensible esas dos filas colisionarían y la
-- migración del perfil de Tecnología fallaría al insertarlas.
CREATE DATABASE ADRTrack COLLATE Modern_Spanish_CI_AS;
GO

USE ADRTrack;
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
    CONSTRAINT CK_PerfilPosicion_Perfil CHECK (Perfil IN (1, 2, 3, 4))   -- 4 = Tecnología (soporte técnico)
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
    -- Movimiento que abrió el caso. NULL identifica la apertura, que es la única fila que
    -- estrena un ticket; las continuaciones lo heredan de ella y por eso lo repiten.
    EnvioEquipoOrigenId INT NULL,
    -- Solo se llenan en la apertura: sellan el cierre del caso cuando ocurre, en vez de tener
    -- que deducirlo recorriendo el historial cada vez que se pregunta.
    FechaCierreCaso DATETIME2(7) NULL,
    MotivoCierreCaso NVARCHAR(300) NULL,
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
    CONSTRAINT FK_EnvioEquipos_Equipos FOREIGN KEY (EquipoId) REFERENCES dbo.Equipos(EquipoId),
    -- Sin cascada: borrar una apertura no debe llevarse por delante las continuaciones que
    -- documentan el recorrido del equipo.
    CONSTRAINT FK_EnvioEquipos_Origen FOREIGN KEY (EnvioEquipoOrigenId) REFERENCES dbo.EnvioEquipos(EnvioEquipoId)
);
GO

-- El ticket es único entre las aperturas, no entre todos los movimientos: dos casos
-- independientes no pueden compartirlo, pero un caso sí puede abarcar varios viajes.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
CREATE UNIQUE INDEX UX_EnvioEquipos_TicketApertura
    ON dbo.EnvioEquipos(NumeroTicket)
    WHERE EnvioEquipoOrigenId IS NULL;
GO
CREATE INDEX IX_EnvioEquipos_Origen ON dbo.EnvioEquipos(EnvioEquipoOrigenId);
GO

CREATE INDEX IX_EnvioEquipos_EquipoId ON dbo.EnvioEquipos(EquipoId);
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
    CONSTRAINT PK_Notificaciones PRIMARY KEY(NotificacionId),
    CONSTRAINT FK_Notificaciones_Envios FOREIGN KEY(EnvioId) REFERENCES dbo.Envios(EnvioId) ON DELETE CASCADE
);
GO
CREATE INDEX IX_Notificaciones_Rol_Fecha ON dbo.Notificaciones(DestinatarioRol,FechaCreacion DESC);
GO

INSERT INTO dbo.EstadosEnvio (Codigo, Nombre, Descripcion, EsFinal, Activo)
VALUES
    (N'EN_FILIAL', N'En filial', N'El envío hacia Tecnología se prepara en la filial.', 0, 1),
    (N'ENTREGADO_TRANSPORTACION', N'Entregado a transportación', N'La filial entregó el envío a transportación.', 0, 1),
    (N'DESPACHADO_TRANSPORTE_PRIVADO', N'Entregado a transporte privado', N'La filial entregó el envío a un responsable privado.', 0, 1),
    (N'PENDIENTE_CONFIRMACION_TRANSPORTE', N'Pendiente de confirmación', N'Transportación debe confirmar la custodia.', 0, 0),
    (N'CONFIRMADO_TRANSPORTACION', N'Confirmado por transportación', N'Transportación confirmó la recepción del envío.', 0, 0),
    (N'EN_TRANSITO', N'En tránsito', N'El envío se encuentra en traslado.', 0, 1),
    (N'RECIBIDO_TRANSPORTACION', N'Recibido por transportación', N'El envío llegó al punto de recepción logística.', 0, 1),
    (N'ESPERA_TECNOLOGIA', N'En espera de Tecnología', N'El envío espera ser procesado por Tecnología.', 0, 1),
    (N'EN_REVISION', N'En proceso de revisión', N'Tecnología está verificando los equipos recibidos.', 0, 1),
    (N'RECIBIDO_TECNOLOGIA', N'Recibido por Tecnología', N'La recepción en Tecnología fue completada.', 1, 1),
    (N'INCIDENCIA_TRANSPORTACION', N'Incidencia en transportación', N'El traslado presenta una incidencia pendiente.', 0, 1),
    (N'PREPARACION_TECNOLOGIA', N'En preparación por Tecnología', N'Tecnología prepara un nuevo envío hacia una filial.', 0, 1),
    (N'DESPACHADO_TECNOLOGIA', N'Despachado por Tecnología', N'Tecnología entregó el envío para su traslado.', 0, 1),
    (N'EN_TRANSPORTACION', N'En espera de asignación de chofer', N'Transportación tiene el envío y debe asignarle chofer; al asignarlo sale a ruta.', 0, 1),
    (N'TRANSPORTE_ASIGNADO', N'Chofer asignado', N'Retirado del flujo: asignar el chofer manda el envío a tránsito.', 0, 0),
    (N'DESPACHADO_TRANSPORTACION', N'Despachado por Transportación', N'Retirado del flujo: el despacho ocurre al asignar el chofer.', 0, 0),
    (N'PENDIENTE_RECEPCION_FILIAL', N'Pendiente de recepción en filial', N'Retirado del flujo: la filial recibe directo desde tránsito.', 0, 0),
    (N'RECIBIDO_FILIAL', N'Recibido en filial', N'La filial recibió el envío conforme. Cierra el flujo y el caso del equipo.', 1, 1),
    -- El envío termina igual; lo que sigue abierto es el caso, para la vuelta siguiente.
    (N'RECIBIDO_FILIAL_INCIDENCIA', N'Recibido en filial con incidencia', N'Llegó, pero algún equipo venía mal. El caso del equipo sigue abierto.', 1, 1),
    (N'RECIBIDO_TECNOLOGIA_INCIDENCIA', N'Recibido por Tecnología con incidencia', N'Llegó a Tecnología, pero algún equipo venía mal.', 1, 1),
    (N'RECEPCION_VALIDADA_FILIAL', N'Recepción validada en filial', N'Retirado del flujo: RECIBIDO_FILIAL es el cierre.', 1, 0);
GO

INSERT INTO dbo.TransicionesEstadoEnvio (EstadoOrigenId, EstadoDestinoId, Activo)
SELECT origen.EstadoEnvioId, destino.EstadoEnvioId, 1
FROM
(
    VALUES
        -- Interno filial -> Tecnología: entregar a Transportación y salir a ruta es un solo
        -- acto, y Tecnología recibe directo desde RECIBIDO_TRANSPORTACION.
        (N'EN_FILIAL', N'ENTREGADO_TRANSPORTACION'),
        (N'ENTREGADO_TRANSPORTACION', N'EN_TRANSITO'),
        (N'EN_TRANSITO', N'RECIBIDO_TRANSPORTACION'),
        (N'RECIBIDO_TRANSPORTACION', N'RECIBIDO_TECNOLOGIA'),
        (N'EN_TRANSITO', N'INCIDENCIA_TRANSPORTACION'),
        (N'INCIDENCIA_TRANSPORTACION', N'EN_TRANSITO'),
        -- Privado: no pasa por Transportación y conserva su ruta por espera y revisión.
        (N'EN_FILIAL', N'DESPACHADO_TRANSPORTE_PRIVADO'),
        (N'DESPACHADO_TRANSPORTE_PRIVADO', N'EN_TRANSITO'),
        (N'EN_TRANSITO', N'ESPERA_TECNOLOGIA'),
        (N'ESPERA_TECNOLOGIA', N'EN_REVISION'),
        (N'EN_REVISION', N'RECIBIDO_TECNOLOGIA'),
        (N'PREPARACION_TECNOLOGIA', N'DESPACHADO_TECNOLOGIA'),
        (N'DESPACHADO_TECNOLOGIA', N'EN_TRANSPORTACION'),
        -- Asignar el chofer ES la salida a ruta, y la filial recibe directo desde tránsito.
        (N'EN_TRANSPORTACION', N'EN_TRANSITO'),
        (N'EN_TRANSITO', N'RECIBIDO_FILIAL'),
        -- Recibir con incidencia es un desenlace distinto, no una variante del mismo estado.
        (N'EN_TRANSITO', N'RECIBIDO_FILIAL_INCIDENCIA'),
        (N'RECIBIDO_TRANSPORTACION', N'RECIBIDO_TECNOLOGIA_INCIDENCIA'),
        (N'EN_REVISION', N'RECIBIDO_TECNOLOGIA_INCIDENCIA')
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
    (N'Santo Domingo Este',            N'SANTO-DOMINGO-ESTE',         31, 1, 1),
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
/*
    Acceso por posición. El token de AuthManager trae "position", y de ahí sale tanto el alcance
    (qué envíos ve) como los permisos (qué puede hacer). Sin estas filas un usuario entra
    autenticado pero sin permisos, y el sistema responde 403 en todo.

    Perfil: 1 = Global (Tecnología), 2 = Transportación, 3 = Filial, 4 = Tecnología (soporte técnico).

    Una posición que falte aquí NO deja al usuario fuera: AlcanceEnvios cae al respaldo
    "tiene affiliate, luego es Filial". Por eso el síntoma no es un 403 sino que el usuario
    aterriza en el módulo equivocado, y si su affiliate es el 30 —la sede, que a propósito no
    está mapeada a ninguna ubicación— el tablero de filial muere con "su filial no está
    asociada a ninguna ubicación". Ese mensaje casi nunca significa lo que dice: significa que
    a esta tabla le falta la fila de su posición.
*/
INSERT INTO dbo.PerfilesPorPosicion (Posicion, Perfil)
VALUES
    (N'Programador Senior',                  1),
    -- Las dos claves que AuthManager emite para Transportación: el rol creado a propósito
    -- para este sistema y el cargo de recursos humanos. Se mapean las dos porque el token
    -- puede traer una sin la otra. El acento no es cosmético: la base es Modern_Spanish_CI_AS,
    -- que ignora mayúsculas pero distingue tildes, así que 'Transportacion' no cruza con
    -- 'Transportación'. Estos textos se copian del token, no se escriben de memoria.
    (N'Encargado Transportación',            2),
    (N'Encargado transportacion y mecanica', 2),
    (N'Administrador de Filial',             3),
    (N'Asistente Administrativo',            3),
    -- Soporte técnico. Mismas dos grafías, y por el mismo motivo: AuthManager emite la
    -- posición unas veces con tilde y otras sin ella, y para esta colación son dos claves
    -- distintas. Con una sola fila, la mitad del equipo entra por el módulo equivocado y el
    -- síntoma es "a unos les funciona y a otros no".
    --
    -- El CHECK de la tabla admite el 4 desde que soporte técnico se separó de Global, pero
    -- este seed se quedó sin las filas: la base nacía soportando el perfil y sin sus datos.
    -- Esa es la causa de que en QA soporte técnico cayera en el tablero de filial.
    (N'Soporte Técnico',                     4),
    (N'Soporte Tecnico',                     4);
GO

INSERT INTO dbo.PermisosPorPosicion (Posicion, Permiso)
VALUES
    -- Tecnología es dueña del sistema: ve todo y administra catálogos.
    (N'Programador Senior', N'envios.consultar'),
    (N'Programador Senior', N'envios.crear'),
    (N'Programador Senior', N'envios.editar'),
    (N'Programador Senior', N'envios.despachar'),
    (N'Programador Senior', N'equipos.gestionar'),
    (N'Programador Senior', N'recepciones.gestionar'),
    (N'Programador Senior', N'incidencias.gestionar'),
    (N'Programador Senior', N'transportes.gestionar'),
    (N'Programador Senior', N'transportes.confirmar'),
    (N'Programador Senior', N'catalogos.administrar'),
    (N'Programador Senior', N'transportes.administrar'),
    -- Transportación solo custodia y confirma; no crea ni edita envíos. 'envios.consultar'
    -- es imprescindible aunque suene de más: su propio módulo lo exige para abrirse.
    (N'Encargado Transportación', N'envios.consultar'),
    (N'Encargado Transportación', N'transportes.gestionar'),
    (N'Encargado Transportación', N'transportes.confirmar'),
    (N'Encargado Transportación', N'incidencias.gestionar'),
    -- La flota es suya: tipos de transporte y choferes internos. Es un permiso aparte de
    -- 'transportes.gestionar' porque ese lo tienen las filiales para asignar transporte a
    -- sus propios envíos, y asignar no es administrar.
    (N'Encargado Transportación', N'transportes.administrar'),
    (N'Encargado transportacion y mecanica', N'envios.consultar'),
    (N'Encargado transportacion y mecanica', N'transportes.gestionar'),
    (N'Encargado transportacion y mecanica', N'transportes.confirmar'),
    (N'Encargado transportacion y mecanica', N'incidencias.gestionar'),
    (N'Encargado transportacion y mecanica', N'transportes.administrar'),
    -- La filial origina envíos y recibe devoluciones, dentro de su propio alcance.
    (N'Administrador de Filial', N'envios.consultar'),
    (N'Administrador de Filial', N'envios.crear'),
    (N'Administrador de Filial', N'envios.editar'),
    (N'Administrador de Filial', N'envios.despachar'),
    (N'Administrador de Filial', N'equipos.gestionar'),
    (N'Administrador de Filial', N'recepciones.gestionar'),
    (N'Administrador de Filial', N'incidencias.gestionar'),
    (N'Administrador de Filial', N'transportes.gestionar'),
    -- El asistente no despacha ni administra inventario.
    (N'Asistente Administrativo', N'envios.consultar'),
    (N'Asistente Administrativo', N'envios.crear'),
    (N'Asistente Administrativo', N'envios.editar'),
    (N'Asistente Administrativo', N'recepciones.gestionar'),
    (N'Asistente Administrativo', N'incidencias.gestionar'),
    (N'Asistente Administrativo', N'transportes.gestionar'),
    -- Soporte técnico trabaja el flujo completo del equipo: lo recibe, lo revisa y lo devuelve.
    -- Quedan fuera a propósito 'catalogos.administrar' y 'transportes.administrar': administrar
    -- el sistema y la flota no es su trabajo, y es lo único que lo separa de Programador Senior.
    -- Las dos grafías llevan el mismo juego, igual que en PerfilesPorPosicion.
    (N'Soporte Técnico', N'envios.consultar'),
    (N'Soporte Técnico', N'envios.crear'),
    (N'Soporte Técnico', N'envios.editar'),
    (N'Soporte Técnico', N'envios.despachar'),
    (N'Soporte Técnico', N'equipos.gestionar'),
    (N'Soporte Técnico', N'recepciones.gestionar'),
    (N'Soporte Técnico', N'incidencias.gestionar'),
    (N'Soporte Tecnico', N'envios.consultar'),
    (N'Soporte Tecnico', N'envios.crear'),
    (N'Soporte Tecnico', N'envios.editar'),
    (N'Soporte Tecnico', N'envios.despachar'),
    (N'Soporte Tecnico', N'equipos.gestionar'),
    (N'Soporte Tecnico', N'recepciones.gestionar'),
    (N'Soporte Tecnico', N'incidencias.gestionar');
GO

INSERT INTO dbo.TiposEquipo (Nombre, Activo)
VALUES (N'Laptop', 1), (N'Computadora de escritorio', 1), (N'Monitor', 1), (N'Impresora', 1), (N'Otro', 1);
GO
