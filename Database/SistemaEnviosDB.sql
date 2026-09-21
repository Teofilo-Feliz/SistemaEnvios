-- Script base de datos ADRTrack. Estructura, datos de referencia y el usuario con que se conecta
-- el API. Un solo archivo: se ejecuta entero y el entorno queda listo.
--
-- ATENCIÓN: este script RECREA la base por completo. Hace DROP DATABASE antes de crearla, así
-- que ejecutarlo sobre un entorno con datos los borra sin aviso. No lo use para actualizar una
-- base existente: para eso están los scripts de Database/Migrations.
--
-- ---------------------------------------------------------------------------------------------
-- ANTES DE EJECUTAR, en un servidor donde el login del API todavía no exista: ponga una
-- contraseña real en @Clave, al final del archivo, en el PASO FINAL. Si el login ya existe no
-- hace falta tocar nada: se reutiliza y no se le cambia la contraseña.
--
-- Si termina con un error rojo en vez de "Comprobacion final OK", la base quedó incompleta.
-- Léalo: dice exactamente qué fila falta.
--
-- Las migraciones de Database/Migrations NO hacen falta en una base nueva: reparan bases ya
-- creadas. Lo que hacían 20260908_UsuarioDelApi y 20260902_PermisosBaseDatos está incorporado
-- aquí, al final, y el resto de sus datos está en los INSERT de este archivo.
-- ---------------------------------------------------------------------------------------------
--
-- DATOS DE PRUEBA
--
-- Al final del archivo hay una seccion que siembra equipos, tecnicos y varios casos de ticket
-- ya montados, para poder recorrer el flujo completo sin cargar nada a mano. Viene APAGADA.
--
-- Para encenderla, ponga 1 en el INSERT de #Opciones, unas lineas mas abajo. En QA y en
-- produccion se deja en 0: son envios inventados y ensucian los listados de todo el mundo.
--
-- Los choferes NO son datos de prueba y se siembran siempre: son los de Transportacion.
-- ---------------------------------------------------------------------------------------------
--
-- EN LOCAL
--
-- La base que crea este archivo se llama ADRTrack. Si su cadena de conexión apunta a otro
-- nombre —los entornos viejos usaban SistemaEnviosDB— el API seguirá hablando con la base
-- vieja y no verá nada de lo que este script cree. Apunte la cadena a ADRTrack:
--
--   dotnet user-secrets set "ConnectionStrings:SistemaEnvios" ^
--     "Server=localhost;Database=ADRTrack;Trusted_Connection=True;TrustServerCertificate=True" ^
--     --project SistemaEnvios.Api
--
-- Para comprobar que la base quedó al nivel del código, apunte las pruebas a ella y ejecútelas.
-- ElModeloCoincideConElEsquemaReal compara tabla por tabla y dice qué columna falta:
--
--   $env:SISTEMAENVIOS_TEST_SQL = "Server=localhost;Database=ADRTrack;Trusted_Connection=True;TrustServerCertificate=True"
--   dotnet test SistemaEnvios.Tests
-- ---------------------------------------------------------------------------------------------
--
-- SOBRE LAS CLAVES DE ACCESO
--
-- La columna se llama Posicion por historia, pero desde que el alcance se resuelve solo con el
-- claim "roles" lo que va ahí son NOMBRES DE ROL de AuthManager. Un cargo de recursos humanos ya
-- no concede nada: no se administra desde AuthManager y por tanto no se puede revocar.
--
-- De las nueve filas sembradas, tres están confirmadas como rol en tokens reales
-- (SuperAdministrador, Soporte Técnico y Encargado Transportación) y dos son posiciones que
-- quedaron inertes: 'Encargada de Soporte Técnico' y 'Encargado transportacion y mecanica'. Se
-- dejan porque no molestan —simplemente no cruzan— y borrarlas antes de confirmar el resto
-- podría dejar a alguien fuera. Las demás están sin confirmar.
--
-- Al crear los grupos en AuthManager, copie el nombre EXACTO del rol aquí. La colación es
-- Modern_Spanish_CI_AS: ignora mayúsculas pero DISTINGUE TILDES, así que 'Soporte Tecnico' y
-- 'Soporte Técnico' son dos claves distintas, y un espacio al final no cruza nunca.
-- ---------------------------------------------------------------------------------------------
USE master;
GO

-- El interruptor vive en una tabla temporal y no en una variable porque tiene que sobrevivir a
-- los GO: una variable muere al final de su lote y esto se lee mil lineas mas abajo.
IF OBJECT_ID(N'tempdb..#Opciones') IS NOT NULL DROP TABLE #Opciones;
CREATE TABLE #Opciones (DatosDePrueba BIT NOT NULL);
INSERT INTO #Opciones (DatosDePrueba) VALUES (0);   -- <<< 1 para sembrar datos de prueba
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
    -- ENV-AAAA-MM-######. Calculada y PERSISTED, no escrita por el API.
    --
    -- La cola es el propio EnvioId: el IDENTITY ya es un contador con candado, así que no hace
    -- falta una tabla de secuencia aparte —que sería leer-y-escribir, o sea una carrera— ni
    -- resolver el primer envío de cada mes, ni el 29 de febrero.
    --
    -- El DATEADD(-4) pasa la marca UTC a hora dominicana. Sin él, todo envío creado después de
    -- las 8 PM se numeraría con la fecha de mañana, y el último día de cada mes —que cae en 28,
    -- 29, 30 o 31 según el mes— con el mes siguiente. Se usa el desfase fijo y no AT TIME ZONE
    -- porque esta última es no determinista y SQL Server no la admite en una columna PERSISTED.
    -- República Dominicana es UTC-4 todo el año: no aplica horario de verano desde 2000.
    --
    -- Va CONVERT con estilo 126 y no FORMAT por lo mismo: FORMAT depende de la cultura.
    --
    -- Por ser PERSISTED, todo INSERT o UPDATE sobre esta tabla exige QUOTED_IDENTIFIER ON. El
    -- API y SSMS lo activan solos; un "sqlcmd -Q" no, y falla con el error 1934.
    NumeroEnvio AS ('ENV-' + LEFT(CONVERT(VARCHAR(10), DATEADD(HOUR, -4, FechaCreacion), 126), 7)
                    + '-' + RIGHT('000000' + CAST(EnvioId AS VARCHAR(10)), 6)) PERSISTED NOT NULL,
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
    -- 1 = Cédula, 2 = Pasaporte. El responsable puede ser extranjero, y su pasaporte lleva letras.
    TipoDocumento TINYINT NOT NULL CONSTRAINT DF_TransportesPrivados_TipoDocumento DEFAULT (1),
    -- 15 es el máximo del pasaporte. La cédula queda acotada a 11 por el CHECK, no por el tipo.
    DocumentoResponsable NVARCHAR(15) NOT NULL,
    PlacaVehiculo NVARCHAR(20) NOT NULL,
    FechaEntrega DATETIME2(7) NULL,
    UsuarioQueEntregoId UNIQUEIDENTIFIER NULL,
    RowVersion ROWVERSION NOT NULL,
    CONSTRAINT PK_TransportesPrivados PRIMARY KEY (TransporteId),
    CONSTRAINT FK_TransportesPrivados_Transportes FOREIGN KEY (TransporteId) REFERENCES dbo.Transportes(TransporteId) ON DELETE CASCADE,
    -- Mismos formatos que aplica FormatosDocumento.cs: cédula 11 dígitos, pasaporte 6-15
    -- alfanumérico en mayúsculas.
    --
    -- El rango va con COLLATE Latin1_General_BIN2 y no con la colación de la base. En LIKE,
    -- '[A-Z]' es un rango en orden de INTERCALACIÓN, y ese orden es a,A,b,B,c,C…, así que con la
    -- colación normal 'A-Z' incluiría casi todas las minúsculas y el CHECK dejaría entrar
    -- 'rd1234567'. Con la binaria el rango sí es el ASCII 65-90.
    CONSTRAINT CK_TransportesPrivados_Documento CHECK
    (
        (TipoDocumento = 1
            AND DocumentoResponsable NOT LIKE '%[^0-9]%'
            AND LEN(DocumentoResponsable) = 11)
     OR (TipoDocumento = 2
            AND DocumentoResponsable COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%'
            AND LEN(DocumentoResponsable) BETWEEN 6 AND 15)
    )
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
        -- Privado: no pasa por Transportación, así que nadie confirma su llegada por él. Cuando
        -- Tecnología lo tiene delante lo recibe en un solo paso, marcando cada equipo conforme o
        -- con incidencia, igual que cualquier otra recepción.
        --
        -- Que EN_TRANSITO pueda cerrar no lo abre a cualquiera: un envío institucional en ese
        -- estado sigue en la carretera y es de Transportación. Quien distingue es
        -- RecepcionService.PermiteVerificar, que admite EN_TRANSITO solo si la estrategia es
        -- EntregaDirectaTecnologia. Estas filas abren el camino; ese guardia decide quién pasa.
        (N'EN_FILIAL', N'DESPACHADO_TRANSPORTE_PRIVADO'),
        (N'DESPACHADO_TRANSPORTE_PRIVADO', N'EN_TRANSITO'),
        (N'EN_TRANSITO', N'RECIBIDO_TECNOLOGIA'),
        (N'EN_TRANSITO', N'RECIBIDO_TECNOLOGIA_INCIDENCIA'),
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
    -- Grupo de seguridad de AuthManager para los super administradores. Llega en el claim
    -- "roles", no en "position": el token trae 'Soporte Técnico,SuperAdministrador' y gana este
    -- por ser el de mayor alcance.
    -- La encargada de soporte técnico es la dueña del sistema. Se registra su cargo además del
    -- grupo, para que el alcance no dependa solo de seguir en un grupo de AuthManager. Ojo: el
    -- cargo va en femenino tal como lo emite AuthManager; si lo ocupa un hombre, el claim dirá
    -- 'Encargado' y esta vía dejará de valer. El rol SuperAdministrador lo sigue cubriendo.
    (N'Encargada de Soporte Técnico',        1),
    (N'SuperAdministrador',                  1),
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
    -- Los otros dos perfiles Global llevan los mismos once permisos de Programador Senior.
    (N'Encargada de Soporte Técnico', N'envios.consultar'),
    (N'Encargada de Soporte Técnico', N'envios.crear'),
    (N'Encargada de Soporte Técnico', N'envios.editar'),
    (N'Encargada de Soporte Técnico', N'envios.despachar'),
    (N'Encargada de Soporte Técnico', N'equipos.gestionar'),
    (N'Encargada de Soporte Técnico', N'recepciones.gestionar'),
    (N'Encargada de Soporte Técnico', N'incidencias.gestionar'),
    (N'Encargada de Soporte Técnico', N'transportes.gestionar'),
    (N'Encargada de Soporte Técnico', N'transportes.confirmar'),
    (N'Encargada de Soporte Técnico', N'transportes.administrar'),
    (N'Encargada de Soporte Técnico', N'catalogos.administrar'),
    (N'SuperAdministrador', N'envios.consultar'),
    (N'SuperAdministrador', N'envios.crear'),
    (N'SuperAdministrador', N'envios.editar'),
    (N'SuperAdministrador', N'envios.despachar'),
    (N'SuperAdministrador', N'equipos.gestionar'),
    (N'SuperAdministrador', N'recepciones.gestionar'),
    (N'SuperAdministrador', N'incidencias.gestionar'),
    (N'SuperAdministrador', N'transportes.gestionar'),
    (N'SuperAdministrador', N'transportes.confirmar'),
    (N'SuperAdministrador', N'transportes.administrar'),
    (N'SuperAdministrador', N'catalogos.administrar'),
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

/* ============================================================================
   COMPROBACIÓN FINAL — no la borre ni la salte

   Existe por un incidente concreto. El CHECK de PerfilesPorPosicion se amplió
   para admitir el perfil 4 (soporte técnico) y el bloque de INSERT de más arriba
   no se actualizó: la base nacía soportando el perfil y sin sus datos. Nada falló
   al crearla. El síntoma apareció semanas después, en producción de QA, como un
   mensaje que hablaba de ubicaciones sin mapear y no de una fila que faltaba.

   Esa es la forma que tienen de fallar estas tablas: en silencio y tarde. Un
   usuario sin fila no recibe un error, cae al respaldo "tiene affiliate, luego es
   Filial" y aterriza en el módulo equivocado con los datos de otro alcance.

   Por eso el script se comprueba a sí mismo antes de darse por bueno. Todo va en
   un solo lote (sin GO) porque @Problemas es una variable de tabla.
   ============================================================================ */
DECLARE @Problemas TABLE (Comprobacion NVARCHAR(70), Detalle NVARCHAR(400));

-- 1) El acceso vive en DOS tablas y hacen cosas distintas: PermisosPorPosicion dice QUÉ puede
--    hacer y PerfilesPorPosicion dice SOBRE CUÁLES envíos. Tener la fila en una sola es el peor
--    estado posible, porque no se parece a un fallo de configuración:
--
--      * permisos sin perfil -> entra, el menú se ve bien, y su alcance es el equivocado.
--      * perfil sin permisos -> entra al módulo correcto y todas las pantallas responden 403.
INSERT INTO @Problemas
SELECT N'Posicion con permisos pero sin perfil', pp.Posicion
FROM (SELECT DISTINCT Posicion FROM dbo.PermisosPorPosicion) AS pp
LEFT JOIN dbo.PerfilesPorPosicion AS pf ON pf.Posicion = pp.Posicion
WHERE pf.Posicion IS NULL;

INSERT INTO @Problemas
SELECT N'Posicion con perfil pero sin permisos', pf.Posicion
FROM dbo.PerfilesPorPosicion AS pf
LEFT JOIN dbo.PermisosPorPosicion AS pp ON pp.Posicion = pf.Posicion
WHERE pp.Posicion IS NULL;

-- 2) Cada perfil que el CHECK admite tiene que tener al menos una posición. Esta es exactamente
--    la comprobación que habría atrapado el incidente el día que se escribió, en vez de semanas
--    después: ampliar el CHECK y olvidar el INSERT deja de pasar desapercibido.
INSERT INTO @Problemas
SELECT N'Perfil admitido por el CHECK y sin ninguna posicion',
       N'Perfil ' + CAST(p.Perfil AS NVARCHAR(3)) + N' (1=Global, 2=Transportacion, 3=Filial, 4=Tecnologia)'
FROM (VALUES (1), (2), (3), (4)) AS p(Perfil)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilesPorPosicion x WHERE x.Perfil = p.Perfil);

-- 3) Un permiso mal escrito no otorga nada y no avisa: PermisosPorPosicionTransformation
--    descarta del token lo que no reconoce, así que 'envios.consutar' se comporta igual que no
--    haber puesto la fila. Esta lista es la de PermissionNames.cs y debe moverse con ella.
INSERT INTO @Problemas
SELECT N'Permiso que no existe en PermissionNames.cs', pp.Permiso
FROM (SELECT DISTINCT Permiso FROM dbo.PermisosPorPosicion) AS pp
WHERE pp.Permiso NOT IN (
    N'envios.consultar', N'envios.crear', N'envios.editar', N'envios.despachar',
    N'equipos.gestionar', N'recepciones.gestionar', N'incidencias.gestionar',
    N'transportes.gestionar', N'transportes.confirmar', N'transportes.administrar',
    N'catalogos.administrar', N'alcance.global');

-- 4) Una clave con un espacio de sobra se ve idéntica en pantalla y no empareja nunca, porque el
--    código compara contra el claim ya pasado por Trim().
--
--    Se compara por DATALENGTH y no con <>: SQL Server ignora los espacios finales al comparar
--    cadenas, así que 'Programador Senior ' <> 'Programador Senior' da FALSO y el problema pasa
--    de largo. Por lo mismo la clave primaria tampoco protege —las dos filas le parecen la misma
--    y aun así entran—, y esta comprobación es lo único que lo detecta.
INSERT INTO @Problemas
SELECT N'Posicion con espacios sobrantes', N'[' + Posicion + N']'
FROM dbo.PerfilesPorPosicion WHERE DATALENGTH(Posicion) <> DATALENGTH(LTRIM(RTRIM(Posicion)))
UNION ALL
SELECT N'Permiso con espacios sobrantes', N'[' + Permiso + N']'
FROM dbo.PermisosPorPosicion WHERE DATALENGTH(Permiso) <> DATALENGTH(LTRIM(RTRIM(Permiso)))
UNION ALL
SELECT N'Posicion con espacios sobrantes', N'[' + Posicion + N']'
FROM dbo.PermisosPorPosicion WHERE DATALENGTH(Posicion) <> DATALENGTH(LTRIM(RTRIM(Posicion)));

-- 5) FilialExternaId es la otra llave del alcance: cruza el claim "affiliate" con la ubicación.
--    Una filial sin él no la alcanza nadie, y el fallo tampoco aparece al crearla sino el día que
--    entra alguien de esa filial. Tecnología es la excepción y va sin id a propósito: lleva el
--    30 en AuthManager, y usarlo aquí le daría alcance de filial a toda la sede.
INSERT INTO @Problemas
SELECT N'Filial sin FilialExternaId (no la alcanza nadie)', Nombre + N' / ' + CodigoCentro
FROM dbo.Ubicaciones WHERE Tipo = 1 AND FilialExternaId IS NULL;

INSERT INTO @Problemas
SELECT N'Ubicacion de Tecnologia con FilialExternaId', Nombre + N' / ' + CodigoCentro
FROM dbo.Ubicaciones WHERE Tipo = 2 AND FilialExternaId IS NOT NULL;

INSERT INTO @Problemas
SELECT N'Debe existir exactamente una ubicacion de Tecnologia',
       N'Encontradas: ' + CAST(COUNT(*) AS NVARCHAR(10))
FROM dbo.Ubicaciones WHERE Tipo = 2
HAVING COUNT(*) <> 1;

/* ------------------------------- Resultado ------------------------------- */

SELECT N'Ubicaciones'          AS Tabla, COUNT(*) AS Filas FROM dbo.Ubicaciones
UNION ALL SELECT N'EstadosEnvio',        COUNT(*) FROM dbo.EstadosEnvio
UNION ALL SELECT N'TransicionesEstado',  COUNT(*) FROM dbo.TransicionesEstadoEnvio
UNION ALL SELECT N'TiposTransporte',     COUNT(*) FROM dbo.TiposTransporte
UNION ALL SELECT N'TiposEquipo',         COUNT(*) FROM dbo.TiposEquipo
UNION ALL SELECT N'PerfilesPorPosicion', COUNT(*) FROM dbo.PerfilesPorPosicion
UNION ALL SELECT N'PermisosPorPosicion', COUNT(*) FROM dbo.PermisosPorPosicion;

SELECT Posicion,
       CASE Perfil WHEN 1 THEN N'Global' WHEN 2 THEN N'Transportacion'
                   WHEN 3 THEN N'Filial' WHEN 4 THEN N'Tecnologia' END AS Perfil,
       (SELECT COUNT(*) FROM dbo.PermisosPorPosicion p WHERE p.Posicion = pf.Posicion) AS Permisos,
       -- Con tilde debe salir 233 en el carácter 10 y sin ella 101. Si sale otro número, el
       -- archivo se ejecutó con una codificación que corrompió la clave y no emparejará jamás.
       UNICODE(SUBSTRING(Posicion, 10, 1)) AS Caracter10
FROM dbo.PerfilesPorPosicion pf
ORDER BY Perfil, Posicion;

IF EXISTS (SELECT 1 FROM @Problemas)
BEGIN
    SELECT Comprobacion, Detalle FROM @Problemas ORDER BY Comprobacion, Detalle;
    RAISERROR(N'La base quedó creada pero INCOMPLETA. Revise la lista de arriba: cada fila es un acceso que fallará en silencio, no al arrancar. Corrija los INSERT de este script y vuelva a ejecutarlo.', 16, 1);
END
ELSE
    PRINT N'Comprobacion final OK: cada posicion tiene perfil y permisos, cada perfil del CHECK esta poblado, y el mapeo de filiales esta completo.';
GO

/* ==============================================================================================
   PASO FINAL: el usuario con el que se conecta el API

   Antes esto vivía en dos migraciones aparte y había que acordarse de ejecutarlas después. Va
   aquí para que levantar un entorno sea un solo archivo.

   El login es del SERVIDOR y no lo arrastra el DROP DATABASE de arriba, así que si ya existe se
   reutiliza: volver a ejecutar este script no le cambia la contraseña a un entorno en marcha.
   ============================================================================================== */

USE master;
GO

DECLARE @Usuario SYSNAME       = N'sistema_envios_app';
DECLARE @Clave   NVARCHAR(128) = N'CAMBIE-ESTA-CLAVE';
DECLARE @sql     NVARCHAR(MAX);

-- Solo hace falta para crearlo. Si el login ya existe, la clave de aquí no se usa ni se comprueba.
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @Usuario)
BEGIN
    IF @Clave = N'CAMBIE-ESTA-CLAVE'
    BEGIN
        RAISERROR(N'El login %s no existe y hay que crearlo. Defina una contraseña real en @Clave, en el PASO FINAL de este script, y vuelva a ejecutarlo.', 16, 1, @Usuario);
        RETURN;
    END

    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@Usuario) +
               N' WITH PASSWORD = ' + QUOTENAME(@Clave, '''') +
               N', CHECK_POLICY = ON, DEFAULT_DATABASE = ' + QUOTENAME(N'ADRTrack') + N';';
    EXEC sp_executesql @sql;
    PRINT N'Login creado.';
END
ELSE
    PRINT N'El login ya existía en el servidor: se reutiliza y no se toca su contraseña.';
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

-- Lectura y escritura de datos, nada de esquema: el API no crea ni altera tablas.
SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@Usuario) + N';
             ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@Usuario) + N';';
EXEC sp_executesql @sql;

-- Y ahora se le quita la escritura sobre las tablas de autoridad.
--
-- Quien pueda escribir en PerfilesPorPosicion o PermisosPorPosicion se otorga a sí mismo alcance
-- Global. El código no puede impedirlo: la mitigación es que el usuario del API no tenga ese
-- permiso. Las mantiene Tecnología por fuera.
SET @sql = N'GRANT SELECT ON dbo.PerfilesPorPosicion TO ' + QUOTENAME(@Usuario) + N';
             GRANT SELECT ON dbo.PermisosPorPosicion TO ' + QUOTENAME(@Usuario) + N';
             DENY INSERT, UPDATE, DELETE ON dbo.PerfilesPorPosicion TO ' + QUOTENAME(@Usuario) + N';
             DENY INSERT, UPDATE, DELETE ON dbo.PermisosPorPosicion TO ' + QUOTENAME(@Usuario) + N';';
EXEC sp_executesql @sql;

-- El mapeo filial -> ubicación es la otra llave del alcance. El API crea ubicaciones pero nunca
-- escribe FilialExternaId, así que se deniega solo esa columna: el mantenimiento normal sigue
-- funcionando y el mapeo queda fuera de su alcance.
SET @sql = N'DENY UPDATE ON dbo.Ubicaciones(FilialExternaId) TO ' + QUOTENAME(@Usuario) + N';';
EXEC sp_executesql @sql;

PRINT N'Usuario del API listo, con lectura de las tablas de autoridad y sin escritura sobre ellas.';
GO

SELECT p.name AS UsuarioDelApi,
       (SELECT COUNT(*) FROM sys.database_permissions d
        WHERE d.grantee_principal_id = p.principal_id AND d.state_desc = 'DENY') AS DenegacionesAplicadas
FROM sys.database_principals p
WHERE p.name = N'sistema_envios_app';
GO

/* ==============================================================================================
   PASO FINAL 2: choferes de Transportacion

   Datos operativos reales, no de prueba, asi que se siembran siempre. Idempotente: se reconocen
   por el numero de empleado.

   El mantenimiento del dia a dia se hace desde la pantalla "Transportes y choferes"; esto es
   solo la carga inicial.
   ============================================================================================== */

USE ADRTrack;
GO

MERGE dbo.ChoferesInternos AS destino
USING (VALUES
    (N'Ramón Castillo Peña',     N'EMP-4101', 1),
    (N'José Luis Encarnación',   N'EMP-4102', 1),
    (N'Wilkin Rosario Núñez',    N'EMP-4103', 1),
    (N'Ángel Manuel Frías',      N'EMP-4104', 1),
    (N'Domingo Reyes Cabrera',   N'EMP-4105', 1),
    (N'Franklin Ureña Santos',   N'EMP-4106', 0)   -- inactivo: no debe poder asignarse
) AS origen (NombreCompleto, NumeroEmpleado, Activo)
ON destino.NumeroEmpleado = origen.NumeroEmpleado
WHEN NOT MATCHED BY TARGET THEN
    INSERT (NombreCompleto, NumeroEmpleado, Activo)
    VALUES (origen.NombreCompleto, origen.NumeroEmpleado, origen.Activo);
GO

/* ==============================================================================================
   PASO FINAL 3: datos de prueba (solo si #Opciones.DatosDePrueba = 1)

   Equipos repartidos entre cuatro filiales y Tecnologia, tecnicos provisionales, y tres casos de
   ticket ya montados para ver la herencia y el descarte sin tener que construirlos a mano:

     1001  ADR-012  caso ABIERTO, 1 movimiento  -> la devolucion hereda 1001 y solo puede ir a
                                                   Santiago. Sale en la pantalla de descarte.
     1002  ADR-015  caso ABIERTO, 3 movimientos -> volvio con incidencia y se reenvio; sirve para
                                                   ver el contador de vueltas.
     1003  ADR-009  caso CERRADO conforme       -> el equipo puede estrenar ticket nuevo.

   ADR-013 y ADR-014 quedan en Tecnologia SIN caso: son asignaciones iniciales y por eso no deben
   aparecer en la pantalla de descarte.

   Los numeros de envio no se escriben: los calcula la base a partir de la fecha y del EnvioId.

   Los tecnicos de UsuariosReferencia son provisionales. Esa tabla deberia poblarse sincronizando
   desde AuthManager y hoy no hay ninguna sincronizacion: solo se lee.
   ============================================================================================== */

IF (SELECT DatosDePrueba FROM #Opciones) = 1
BEGIN
    BEGIN TRANSACTION;

    INSERT INTO dbo.Equipos (CodigoActivo, NumeroSerie, TipoEquipoId, UbicacionActualId, Marca, Modelo, Observaciones)
    SELECT e.CodigoActivo, e.NumeroSerie, te.TipoEquipoId, u.UbicacionId, e.Marca, e.Modelo, e.Observaciones
    FROM (VALUES
        (N'ADR-001', N'SN-DL-77120', N'Laptop',                    N'SANTIAGO',           N'Dell',    N'Latitude 5440',  N'Equipo de recepción'),
        (N'ADR-002', N'SN-DL-77121', N'Laptop',                    N'SANTIAGO',           N'Dell',    N'Latitude 5440',  N'Equipo de caja'),
        (N'ADR-003', N'SN-HP-45012', N'Impresora',                 N'SANTIAGO',           N'HP',      N'LaserJet M404',  N'Impresora de archivo'),
        (N'ADR-004', N'SN-LN-33450', N'Computadora de escritorio', N'SANTO-DOMINGO-ESTE', N'Lenovo',  N'ThinkCentre M70',N'Consultorio 2'),
        (N'ADR-005', N'SN-LN-33451', N'Computadora de escritorio', N'SANTO-DOMINGO-ESTE', N'Lenovo',  N'ThinkCentre M70',N'Consultorio 3'),
        (N'ADR-006', N'SN-SM-90881', N'Monitor',                   N'SANTO-DOMINGO-ESTE', N'Samsung', N'S24R350',        N'Monitor de reemplazo'),
        (N'ADR-007', N'SN-DL-77122', N'Laptop',                    N'LA-VEGA',            N'Dell',    N'Vostro 3520',    N'Administración'),
        (N'ADR-008', N'SN-HP-45013', N'Impresora',                 N'LA-VEGA',            N'HP',      N'LaserJet M404',  N'Recepción'),
        (N'ADR-009', N'SN-LN-33452', N'Computadora de escritorio', N'SAN-CRISTOBAL',      N'Lenovo',  N'ThinkCentre M70',N'Facturación'),
        (N'ADR-010', N'SN-SM-90882', N'Monitor',                   N'SAN-CRISTOBAL',      N'Samsung', N'S24R350',        N'Facturación'),
        (N'ADR-011', N'SN-GN-10001', N'Otro',                      N'SAN-CRISTOBAL',      N'APC',     N'Back-UPS 650',   N'UPS con batería agotada'),
        (N'ADR-012', N'SN-DL-77123', N'Laptop',                    N'TECNOLOGIA',         N'Dell',    N'Latitude 5440',  N'Reparado, listo para devolver'),
        (N'ADR-013', N'SN-LN-33453', N'Computadora de escritorio', N'TECNOLOGIA',         N'Lenovo',  N'ThinkCentre M70',N'Equipo nuevo para asignar'),
        (N'ADR-014', N'SN-SM-90883', N'Monitor',                   N'TECNOLOGIA',         N'Samsung', N'S24R350',        N'Equipo nuevo para asignar'),
        (N'ADR-015', N'SN-HP-45014', N'Impresora',                 N'TECNOLOGIA',         N'HP',      N'LaserJet M404',  N'Reparada, lista para devolver')
    ) AS e (CodigoActivo, NumeroSerie, TipoNombre, CodigoCentro, Marca, Modelo, Observaciones)
    JOIN dbo.TiposEquipo  te ON te.Nombre = e.TipoNombre
    JOIN dbo.Ubicaciones  u  ON u.CodigoCentro = e.CodigoCentro
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Equipos x WHERE x.CodigoActivo = e.CodigoActivo);

    MERGE dbo.UsuariosReferencia AS destino
    USING (VALUES
        ('b7d1a2c4-5e60-4f83-9a11-2c3d4e5f6071', N'Laura Méndez Polanco',  N'TEC-1001', N'lmendez@rehabilitacion.org.do',  1, 1),
        ('c8e2b3d5-6f71-4094-8b22-3d4e5f607182', N'Roberto Díaz Vásquez',  N'TEC-1002', N'rdiaz@rehabilitacion.org.do',    1, 1),
        ('d9f3c4e6-7082-41a5-9c33-4e5f60718293', N'Yuderka Santana Cruz',  N'TEC-1003', N'ysantana@rehabilitacion.org.do', 1, 1),
        ('e0a4d5f7-8193-42b6-8d44-5f6071829304', N'Técnico dado de baja',  N'TEC-0099', NULL,                             1, 0)
    ) AS origen (UsuarioExternoId, NombreCompleto, NumeroEmpleado, Correo, EsTecnico, Activo)
    ON destino.UsuarioExternoId = origen.UsuarioExternoId
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (UsuarioExternoId, NombreCompleto, NumeroEmpleado, Correo, EsTecnico, Activo, FechaUltimaSincronizacion, Origen)
        VALUES (origen.UsuarioExternoId, origen.NombreCompleto, origen.NumeroEmpleado, origen.Correo,
                origen.EsTecnico, origen.Activo, SYSUTCDATETIME(), N'MANUAL');

    DECLARE @Usuario UNIQUEIDENTIFIER = '11111111-2222-3333-4444-555555555555';
    DECLARE @Tecnologia INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'TECNOLOGIA');
    DECLARE @Santiago   INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'SANTIAGO');
    DECLARE @LaVega     INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'LA-VEGA');
    DECLARE @SanCris    INT = (SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro = N'SAN-CRISTOBAL');
    DECLARE @EnTecnologia   INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'RECIBIDO_TECNOLOGIA');
    DECLARE @RecibidoFilial INT = (SELECT EstadoEnvioId FROM dbo.EstadosEnvio WHERE Codigo = N'RECIBIDO_FILIAL');

    IF NOT EXISTS (SELECT 1 FROM dbo.EnvioEquipos WHERE NumeroTicket = N'1001')
    BEGIN
        DECLARE @EnvioA INT;

        -- 1001: Santiago mando el equipo y Tecnologia ya lo recibio. Caso abierto.
        INSERT INTO dbo.Envios (UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        VALUES (@Santiago, @Tecnologia, @EnTecnologia, 1, @Usuario, N'Laptop con fallo de teclado.', DATEADD(DAY, -6, SYSUTCDATETIME()));
        SET @EnvioA = SCOPE_IDENTITY();
        INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        SELECT @EnvioA, EquipoId, N'1001', @Usuario, N'Apertura del caso.', DATEADD(DAY, -6, SYSUTCDATETIME())
        FROM dbo.Equipos WHERE CodigoActivo = N'ADR-012';
        INSERT INTO dbo.HistorialesEstadoEnvio (EnvioId, EstadoEnvioId, Fecha, UbicacionId, UsuarioId, Observaciones)
        VALUES (@EnvioA, @EnTecnologia, DATEADD(DAY, -5, SYSUTCDATETIME()), @Tecnologia, @Usuario, N'Recibido por Tecnologia.');

        -- 1002: La Vega. Fue, volvio con incidencia y se reenvio: tres movimientos, mismo ticket.
        DECLARE @EnvioB INT, @EnvioB2 INT, @EnvioB3 INT, @AperturaB INT;
        INSERT INTO dbo.Envios (UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        VALUES (@LaVega, @Tecnologia, @EnTecnologia, 1, @Usuario, N'Impresora no imprime.', DATEADD(DAY, -20, SYSUTCDATETIME()));
        SET @EnvioB = SCOPE_IDENTITY();
        INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        SELECT @EnvioB, EquipoId, N'1002', @Usuario, N'Apertura del caso.', DATEADD(DAY, -20, SYSUTCDATETIME())
        FROM dbo.Equipos WHERE CodigoActivo = N'ADR-015';
        SET @AperturaB = SCOPE_IDENTITY();

        INSERT INTO dbo.Envios (UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        VALUES (@Tecnologia, @LaVega, @RecibidoFilial, 2, @Usuario, N'Devolucion tras reparacion.', DATEADD(DAY, -14, SYSUTCDATETIME()));
        SET @EnvioB2 = SCOPE_IDENTITY();
        INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, EnvioEquipoOrigenId, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        SELECT @EnvioB2, EquipoId, N'1002', @AperturaB, @Usuario, N'Devolucion: llego con incidencia.', DATEADD(DAY, -14, SYSUTCDATETIME())
        FROM dbo.Equipos WHERE CodigoActivo = N'ADR-015';

        INSERT INTO dbo.Envios (UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        VALUES (@LaVega, @Tecnologia, @EnTecnologia, 1, @Usuario, N'Reenvio: el problema no se resolvio.', DATEADD(DAY, -9, SYSUTCDATETIME()));
        SET @EnvioB3 = SCOPE_IDENTITY();
        INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, EnvioEquipoOrigenId, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        SELECT @EnvioB3, EquipoId, N'1002', @AperturaB, @Usuario, N'Reenvio por incidencia.', DATEADD(DAY, -9, SYSUTCDATETIME())
        FROM dbo.Equipos WHERE CodigoActivo = N'ADR-015';

        -- 1003: ciclo completo cerrado conforme. El equipo ya volvio a San Cristobal.
        DECLARE @EnvioC INT, @EnvioC2 INT, @AperturaC INT;
        INSERT INTO dbo.Envios (UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion, FechaFinalizacion)
        VALUES (@SanCris, @Tecnologia, @EnTecnologia, 1, @Usuario, N'PC no enciende.', DATEADD(DAY, -30, SYSUTCDATETIME()), NULL);
        SET @EnvioC = SCOPE_IDENTITY();
        INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, UsuarioSolicitanteId, Observaciones, FechaCreacion, FechaCierreCaso, MotivoCierreCaso)
        SELECT @EnvioC, EquipoId, N'1003', @Usuario, N'Apertura del caso.', DATEADD(DAY, -30, SYSUTCDATETIME()),
               DATEADD(DAY, -22, SYSUTCDATETIME()), N'Recibido conforme en la filial.'
        FROM dbo.Equipos WHERE CodigoActivo = N'ADR-009';
        SET @AperturaC = SCOPE_IDENTITY();

        INSERT INTO dbo.Envios (UbicacionOrigenId, UbicacionDestinoId, EstadoEnvioId, Direccion, UsuarioSolicitanteId, Observaciones, FechaCreacion, FechaFinalizacion)
        VALUES (@Tecnologia, @SanCris, @RecibidoFilial, 2, @Usuario, N'Devolucion reparada.', DATEADD(DAY, -24, SYSUTCDATETIME()), DATEADD(DAY, -22, SYSUTCDATETIME()));
        SET @EnvioC2 = SCOPE_IDENTITY();
        INSERT INTO dbo.EnvioEquipos (EnvioId, EquipoId, NumeroTicket, EnvioEquipoOrigenId, UsuarioSolicitanteId, Observaciones, FechaCreacion)
        SELECT @EnvioC2, EquipoId, N'1003', @AperturaC, @Usuario, N'Devolucion recibida conforme.', DATEADD(DAY, -24, SYSUTCDATETIME())
        FROM dbo.Equipos WHERE CodigoActivo = N'ADR-009';
    END

    COMMIT TRANSACTION;
    PRINT N'Datos de prueba sembrados.';
END
ELSE
    PRINT N'Sin datos de prueba. Para sembrarlos, ponga 1 en el INSERT de #Opciones, al inicio.';
GO

/* ----------------------------------------------------------------------------------------------
   Con que queda la base.
   ---------------------------------------------------------------------------------------------- */
SELECT 'Filiales'           AS Dato, COUNT(*) AS Total FROM dbo.Ubicaciones WHERE Tipo = 1
UNION ALL SELECT 'Tecnologia',         COUNT(*) FROM dbo.Ubicaciones WHERE Tipo = 2
UNION ALL SELECT 'Estados activos',    COUNT(*) FROM dbo.EstadosEnvio WHERE Activo = 1
UNION ALL SELECT 'Transiciones',       COUNT(*) FROM dbo.TransicionesEstadoEnvio WHERE Activo = 1
UNION ALL SELECT 'Choferes activos',   COUNT(*) FROM dbo.ChoferesInternos WHERE Activo = 1
UNION ALL SELECT 'Choferes inactivos', COUNT(*) FROM dbo.ChoferesInternos WHERE Activo = 0
UNION ALL SELECT 'Tecnicos activos',   COUNT(*) FROM dbo.UsuariosReferencia WHERE EsTecnico = 1 AND Activo = 1
UNION ALL SELECT 'Equipos',            COUNT(*) FROM dbo.Equipos
UNION ALL SELECT 'Envios',             COUNT(*) FROM dbo.Envios;
GO

DROP TABLE #Opciones;
GO
