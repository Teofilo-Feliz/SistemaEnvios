-- El claim "position" de AuthManager es lo que decide QUE ve un usuario y QUE puede hacer.
-- No se usa la ubicacion: en la sede conviven Tecnologia, administradores de filial y
-- asistentes administrativos con el mismo affiliate, y deducir el perfil del centro le daria
-- alcance global a todo el centro.
--
-- Perfil: 1 = Global (Tecnologia, ve todo y filtra por filial)
--         2 = Transportacion (solo sus etapas, todas las filiales, transporte institucional)
--         3 = Filial (solo los envios de su filial)
USE SistemaEnviosDB;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PerfilesPorPosicion', N'U') IS NULL
    CREATE TABLE dbo.PerfilesPorPosicion
    (
        Posicion NVARCHAR(150) NOT NULL,
        Perfil   TINYINT       NOT NULL,
        CONSTRAINT PK_PerfilesPorPosicion PRIMARY KEY (Posicion),
        CONSTRAINT CK_PerfilPosicion_Perfil CHECK (Perfil IN (1, 2, 3))
    );

IF OBJECT_ID(N'dbo.PermisosPorPosicion', N'U') IS NULL
    CREATE TABLE dbo.PermisosPorPosicion
    (
        Posicion NVARCHAR(150) NOT NULL,
        Permiso  NVARCHAR(50)  NOT NULL,
        CONSTRAINT PK_PermisosPorPosicion PRIMARY KEY (Posicion, Permiso)
    );

-- ----------------------------------------------------------------------------------
-- MAPEO. "Programador Senior" es la unica posicion vista en un token real. El resto son
-- PLANTILLAS: reemplazar por los nombres exactos que AuthManager emita en "position".
-- Una posicion sin mapear no recibe permisos, asi que la API le respondera 403.
-- ----------------------------------------------------------------------------------
MERGE dbo.PerfilesPorPosicion AS destino
USING (VALUES
    (N'Programador Senior',            1),
    (N'Encargado de Transportacion',   2),
    (N'Administrador de Filial',       3),
    (N'Asistente Administrativo',      3)
) AS origen (Posicion, Perfil)
ON destino.Posicion = origen.Posicion
WHEN NOT MATCHED BY TARGET THEN INSERT (Posicion, Perfil) VALUES (origen.Posicion, origen.Perfil);

MERGE dbo.PermisosPorPosicion AS destino
USING (VALUES
    -- Tecnologia: dueno del sistema.
    (N'Programador Senior', N'envios.consultar'),
    (N'Programador Senior', N'envios.crear'),
    (N'Programador Senior', N'envios.editar'),
    (N'Programador Senior', N'envios.despachar'),
    (N'Programador Senior', N'transportes.gestionar'),
    (N'Programador Senior', N'transportes.confirmar'),
    (N'Programador Senior', N'recepciones.gestionar'),
    (N'Programador Senior', N'incidencias.gestionar'),
    (N'Programador Senior', N'equipos.gestionar'),
    (N'Programador Senior', N'catalogos.administrar'),

    -- Transportacion: unica posicion que confirma custodia y llegada.
    (N'Encargado de Transportacion', N'envios.consultar'),
    (N'Encargado de Transportacion', N'transportes.gestionar'),
    (N'Encargado de Transportacion', N'transportes.confirmar'),
    (N'Encargado de Transportacion', N'incidencias.gestionar'),

    -- Administrador de filial: opera su filial de punta a punta.
    (N'Administrador de Filial', N'envios.consultar'),
    (N'Administrador de Filial', N'envios.crear'),
    (N'Administrador de Filial', N'envios.editar'),
    (N'Administrador de Filial', N'envios.despachar'),
    (N'Administrador de Filial', N'transportes.gestionar'),
    (N'Administrador de Filial', N'recepciones.gestionar'),
    (N'Administrador de Filial', N'incidencias.gestionar'),
    (N'Administrador de Filial', N'equipos.gestionar'),

    -- Asistente administrativo: registra y da seguimiento, no despacha.
    (N'Asistente Administrativo', N'envios.consultar'),
    (N'Asistente Administrativo', N'envios.crear'),
    (N'Asistente Administrativo', N'envios.editar'),
    (N'Asistente Administrativo', N'transportes.gestionar'),
    (N'Asistente Administrativo', N'recepciones.gestionar'),
    (N'Asistente Administrativo', N'incidencias.gestionar')
) AS origen (Posicion, Permiso)
ON destino.Posicion = origen.Posicion AND destino.Permiso = origen.Permiso
WHEN NOT MATCHED BY TARGET THEN INSERT (Posicion, Permiso) VALUES (origen.Posicion, origen.Permiso);

COMMIT TRANSACTION;
GO
