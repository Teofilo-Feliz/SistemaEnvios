IF DB_ID(N'SistemaEnviosDB') IS NULL
BEGIN
    CREATE DATABASE [SistemaEnviosDB];
END;
GO

USE [SistemaEnviosDB];
GO
CREATE TABLE [EstadosEnvio] (
        [EstadoEnvioId] int NOT NULL IDENTITY,
        [Codigo] nvarchar(50) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Descripcion] nvarchar(500) NULL,
        [EsFinal] bit NOT NULL,
        [Activo] bit NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_EstadosEnvio] PRIMARY KEY ([EstadoEnvioId])
    );
CREATE TABLE [TiposEquipo] (
        [TipoEquipoId] int NOT NULL IDENTITY,
        [Nombre] nvarchar(100) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_TiposEquipo] PRIMARY KEY ([TipoEquipoId])
    );
CREATE TABLE [Ubicaciones] (
        [UbicacionId] int NOT NULL IDENTITY,
        [Nombre] nvarchar(150) NOT NULL,
        [CodigoCentro] nvarchar(50) NOT NULL,
        [Activo] bit NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Ubicaciones] PRIMARY KEY ([UbicacionId])
    );
CREATE TABLE [TransicionesEstadoEnvio] (
        [TransicionEstadoEnvioId] int NOT NULL IDENTITY,
        [EstadoOrigenId] int NOT NULL,
        [EstadoDestinoId] int NOT NULL,
        [Activo] bit NOT NULL,
        CONSTRAINT [PK_TransicionesEstadoEnvio] PRIMARY KEY ([TransicionEstadoEnvioId]),
        CONSTRAINT [FK_TransicionesEstadoEnvio_EstadosEnvio_EstadoDestinoId] FOREIGN KEY ([EstadoDestinoId]) REFERENCES [EstadosEnvio] ([EstadoEnvioId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TransicionesEstadoEnvio_EstadosEnvio_EstadoOrigenId] FOREIGN KEY ([EstadoOrigenId]) REFERENCES [EstadosEnvio] ([EstadoEnvioId]) ON DELETE NO ACTION
    );
CREATE TABLE [Envios] (
        [EnvioId] int NOT NULL IDENTITY,
        [NumeroEnvio] nvarchar(30) NOT NULL,
        [UbicacionOrigenId] int NOT NULL,
        [UbicacionDestinoId] int NOT NULL,
        [EstadoEnvioId] int NOT NULL,
        [UsuarioSolicitanteId] uniqueidentifier NOT NULL,
        [FechaFinalizacion] datetime2 NULL,
        [Observaciones] nvarchar(2000) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Envios] PRIMARY KEY ([EnvioId]),
        CONSTRAINT [CK_Envio_UbicacionesDistintas] CHECK (UbicacionOrigenId <> UbicacionDestinoId),
        CONSTRAINT [FK_Envios_EstadosEnvio_EstadoEnvioId] FOREIGN KEY ([EstadoEnvioId]) REFERENCES [EstadosEnvio] ([EstadoEnvioId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Envios_Ubicaciones_UbicacionDestinoId] FOREIGN KEY ([UbicacionDestinoId]) REFERENCES [Ubicaciones] ([UbicacionId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Envios_Ubicaciones_UbicacionOrigenId] FOREIGN KEY ([UbicacionOrigenId]) REFERENCES [Ubicaciones] ([UbicacionId]) ON DELETE NO ACTION
    );
CREATE TABLE [Equipos] (
        [EquipoId] int NOT NULL IDENTITY,
        [CodigoActivo] nvarchar(50) NULL,
        [NumeroSerie] nvarchar(100) NULL,
        [TipoEquipoId] int NOT NULL,
        [UbicacionActualId] int NOT NULL,
        [Marca] nvarchar(100) NOT NULL,
        [Modelo] nvarchar(100) NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Equipos] PRIMARY KEY ([EquipoId]),
        CONSTRAINT [FK_Equipos_TiposEquipo_TipoEquipoId] FOREIGN KEY ([TipoEquipoId]) REFERENCES [TiposEquipo] ([TipoEquipoId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Equipos_Ubicaciones_UbicacionActualId] FOREIGN KEY ([UbicacionActualId]) REFERENCES [Ubicaciones] ([UbicacionId]) ON DELETE NO ACTION
    );
CREATE TABLE [HistorialesEstadoEnvio] (
        [HistorialEstadoEnvioId] int NOT NULL IDENTITY,
        [EnvioId] int NOT NULL,
        [EstadoEnvioId] int NOT NULL,
        [Fecha] datetime2 NOT NULL,
        [UbicacionId] int NOT NULL,
        [UsuarioId] uniqueidentifier NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_HistorialesEstadoEnvio] PRIMARY KEY ([HistorialEstadoEnvioId]),
        CONSTRAINT [FK_HistorialesEstadoEnvio_Envios_EnvioId] FOREIGN KEY ([EnvioId]) REFERENCES [Envios] ([EnvioId]) ON DELETE CASCADE,
        CONSTRAINT [FK_HistorialesEstadoEnvio_EstadosEnvio_EstadoEnvioId] FOREIGN KEY ([EstadoEnvioId]) REFERENCES [EstadosEnvio] ([EstadoEnvioId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_HistorialesEstadoEnvio_Ubicaciones_UbicacionId] FOREIGN KEY ([UbicacionId]) REFERENCES [Ubicaciones] ([UbicacionId]) ON DELETE NO ACTION
    );
CREATE TABLE [Recepciones] (
        [RecepcionId] int NOT NULL IDENTITY,
        [EnvioId] int NOT NULL,
        [TecnicoAsignadoId] uniqueidentifier NULL,
        [UsuarioQueRecibioId] uniqueidentifier NULL,
        [FechaAsignacion] datetime2 NULL,
        [FechaRecepcion] datetime2 NULL,
        [EstadoRecepcion] int NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [UsuarioQueAsignoId] uniqueidentifier NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Recepciones] PRIMARY KEY ([RecepcionId]),
        CONSTRAINT [FK_Recepciones_Envios_EnvioId] FOREIGN KEY ([EnvioId]) REFERENCES [Envios] ([EnvioId]) ON DELETE CASCADE
    );
CREATE TABLE [Transportes] (
        [TransporteId] int NOT NULL IDENTITY,
        [EnvioId] int NOT NULL,
        [Tipo] nvarchar(50) NOT NULL,
        [NombreChofer] nvarchar(150) NULL,
        [Placa] nvarchar(20) NULL,
        [FechaEntregaTransportacion] datetime2 NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [EntregaConfirmada] bit NOT NULL,
        [FechaConfirmacionEntrega] datetime2 NULL,
        [UsuarioConfirmacionId] uniqueidentifier NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Transportes] PRIMARY KEY ([TransporteId]),
        CONSTRAINT [CK_Transporte_Confirmacion] CHECK ((EntregaConfirmada = 0 AND FechaConfirmacionEntrega IS NULL AND UsuarioConfirmacionId IS NULL) OR (EntregaConfirmada = 1 AND FechaConfirmacionEntrega IS NOT NULL AND UsuarioConfirmacionId IS NOT NULL)),
        CONSTRAINT [FK_Transportes_Envios_EnvioId] FOREIGN KEY ([EnvioId]) REFERENCES [Envios] ([EnvioId]) ON DELETE CASCADE
    );
CREATE TABLE [EnvioEquipos] (
        [EnvioEquipoId] int NOT NULL IDENTITY,
        [EnvioId] int NOT NULL,
        [EquipoId] int NOT NULL,
        [NumeroTicket] nvarchar(50) NOT NULL,
        [UsuarioSolicitanteId] uniqueidentifier NOT NULL,
        [Observaciones] nvarchar(2000) NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_EnvioEquipos] PRIMARY KEY ([EnvioEquipoId]),
        CONSTRAINT [FK_EnvioEquipos_Envios_EnvioId] FOREIGN KEY ([EnvioId]) REFERENCES [Envios] ([EnvioId]) ON DELETE CASCADE,
        CONSTRAINT [FK_EnvioEquipos_Equipos_EquipoId] FOREIGN KEY ([EquipoId]) REFERENCES [Equipos] ([EquipoId]) ON DELETE NO ACTION
    );
CREATE TABLE [Incidencias] (
        [IncidenciaId] int NOT NULL IDENTITY,
        [EnvioId] int NOT NULL,
        [EnvioEquipoId] int NULL,
        [TransporteId] int NULL,
        [Descripcion] nvarchar(2000) NOT NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_Incidencias] PRIMARY KEY ([IncidenciaId]),
        CONSTRAINT [FK_Incidencias_EnvioEquipos_EnvioEquipoId] FOREIGN KEY ([EnvioEquipoId]) REFERENCES [EnvioEquipos] ([EnvioEquipoId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Incidencias_Envios_EnvioId] FOREIGN KEY ([EnvioId]) REFERENCES [Envios] ([EnvioId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Incidencias_Transportes_TransporteId] FOREIGN KEY ([TransporteId]) REFERENCES [Transportes] ([TransporteId]) ON DELETE NO ACTION
    );
CREATE TABLE [RecepcionEquipos] (
        [RecepcionEquipoId] int NOT NULL IDENTITY,
        [RecepcionId] int NOT NULL,
        [EnvioEquipoId] int NOT NULL,
        [EstadoRecepcionEquipo] int NOT NULL,
        [FechaVerificacion] datetime2 NULL,
        [Observaciones] nvarchar(2000) NULL,
        [FechaCreacion] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UsuarioCreacionId] uniqueidentifier NULL,
        [FechaModificacion] datetime2 NULL,
        [UsuarioModificacionId] uniqueidentifier NULL,
        CONSTRAINT [PK_RecepcionEquipos] PRIMARY KEY ([RecepcionEquipoId]),
        CONSTRAINT [FK_RecepcionEquipos_EnvioEquipos_EnvioEquipoId] FOREIGN KEY ([EnvioEquipoId]) REFERENCES [EnvioEquipos] ([EnvioEquipoId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RecepcionEquipos_Recepciones_RecepcionId] FOREIGN KEY ([RecepcionId]) REFERENCES [Recepciones] ([RecepcionId]) ON DELETE CASCADE
    );
CREATE UNIQUE INDEX [IX_EnvioEquipos_EnvioId_EquipoId] ON [EnvioEquipos] ([EnvioId], [EquipoId]);
CREATE INDEX [IX_EnvioEquipos_EquipoId] ON [EnvioEquipos] ([EquipoId]);
CREATE INDEX [IX_Envios_EstadoEnvioId] ON [Envios] ([EstadoEnvioId]);
CREATE UNIQUE INDEX [IX_Envios_NumeroEnvio] ON [Envios] ([NumeroEnvio]);
CREATE INDEX [IX_Envios_UbicacionDestinoId] ON [Envios] ([UbicacionDestinoId]);
CREATE INDEX [IX_Envios_UbicacionOrigenId] ON [Envios] ([UbicacionOrigenId]);
EXEC(N'CREATE UNIQUE INDEX [IX_Equipos_CodigoActivo] ON [Equipos] ([CodigoActivo]) WHERE [CodigoActivo] IS NOT NULL');
EXEC(N'CREATE UNIQUE INDEX [IX_Equipos_NumeroSerie] ON [Equipos] ([NumeroSerie]) WHERE [NumeroSerie] IS NOT NULL');
CREATE INDEX [IX_Equipos_TipoEquipoId] ON [Equipos] ([TipoEquipoId]);
CREATE INDEX [IX_Equipos_UbicacionActualId] ON [Equipos] ([UbicacionActualId]);
CREATE UNIQUE INDEX [IX_EstadosEnvio_Codigo] ON [EstadosEnvio] ([Codigo]);
CREATE INDEX [IX_HistorialesEstadoEnvio_EnvioId] ON [HistorialesEstadoEnvio] ([EnvioId]);
CREATE INDEX [IX_HistorialesEstadoEnvio_EstadoEnvioId] ON [HistorialesEstadoEnvio] ([EstadoEnvioId]);
CREATE INDEX [IX_HistorialesEstadoEnvio_UbicacionId] ON [HistorialesEstadoEnvio] ([UbicacionId]);
CREATE INDEX [IX_Incidencias_EnvioEquipoId] ON [Incidencias] ([EnvioEquipoId]);
CREATE INDEX [IX_Incidencias_EnvioId] ON [Incidencias] ([EnvioId]);
CREATE INDEX [IX_Incidencias_TransporteId] ON [Incidencias] ([TransporteId]);
CREATE UNIQUE INDEX [IX_RecepcionEquipos_EnvioEquipoId] ON [RecepcionEquipos] ([EnvioEquipoId]);
CREATE INDEX [IX_RecepcionEquipos_RecepcionId] ON [RecepcionEquipos] ([RecepcionId]);
CREATE UNIQUE INDEX [IX_Recepciones_EnvioId] ON [Recepciones] ([EnvioId]);
CREATE UNIQUE INDEX [IX_TiposEquipo_Nombre] ON [TiposEquipo] ([Nombre]);
CREATE INDEX [IX_TransicionesEstadoEnvio_EstadoDestinoId] ON [TransicionesEstadoEnvio] ([EstadoDestinoId]);
CREATE UNIQUE INDEX [IX_TransicionesEstadoEnvio_EstadoOrigenId_EstadoDestinoId] ON [TransicionesEstadoEnvio] ([EstadoOrigenId], [EstadoDestinoId]);
CREATE UNIQUE INDEX [IX_Transportes_EnvioId] ON [Transportes] ([EnvioId]);
CREATE UNIQUE INDEX [IX_Ubicaciones_CodigoCentro] ON [Ubicaciones] ([CodigoCentro]);

GO

