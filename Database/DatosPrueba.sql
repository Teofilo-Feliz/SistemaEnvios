/* Datos de demostración para SistemaEnviosDB. Reejecutable: reemplaza solo registros TEST-. */
USE SistemaEnviosDB;
GO
SET XACT_ABORT ON;
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;
DECLARE @Usuario UNIQUEIDENTIFIER='11111111-1111-1111-1111-111111111111';

MERGE dbo.UsuariosReferencia AS destino
USING (VALUES
 ('22222222-2222-2222-2222-222222222222',N'Laura Méndez',N'TEC-1001',N'laura.mendez@institucion.local',1,1),
 ('33333333-3333-3333-3333-333333333333',N'Roberto Díaz',N'TEC-1002',N'roberto.diaz@institucion.local',1,1),
 ('44444444-4444-4444-4444-444444444444',N'Técnico inactivo',N'TEC-0099',N'inactivo@institucion.local',1,0)
) AS origen(Id,Nombre,Empleado,Correo,EsTecnico,Activo)
ON destino.UsuarioExternoId=CONVERT(uniqueidentifier,origen.Id)
WHEN MATCHED THEN UPDATE SET NombreCompleto=origen.Nombre,NumeroEmpleado=origen.Empleado,Correo=origen.Correo,EsTecnico=origen.EsTecnico,Activo=origen.Activo,FechaUltimaSincronizacion=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(UsuarioExternoId,NombreCompleto,NumeroEmpleado,Correo,EsTecnico,Activo,FechaUltimaSincronizacion,Origen) VALUES(CONVERT(uniqueidentifier,origen.Id),origen.Nombre,origen.Empleado,origen.Correo,origen.EsTecnico,origen.Activo,SYSUTCDATETIME(),N'AUTHMANAGER');

DELETE FROM dbo.Envios WHERE NumeroEnvio LIKE N'TEST-%';
DELETE FROM dbo.Equipos WHERE CodigoActivo LIKE N'TEST-%';

IF NOT EXISTS (SELECT 1 FROM dbo.Ubicaciones WHERE CodigoCentro=N'FILIAL-SANTIAGO')
    INSERT dbo.Ubicaciones(Nombre,CodigoCentro,Tipo,Activo) VALUES(N'Filial Santiago',N'FILIAL-SANTIAGO',1,1);
IF NOT EXISTS (SELECT 1 FROM dbo.Ubicaciones WHERE CodigoCentro=N'FILIAL-SANTO-DOMINGO')
    INSERT dbo.Ubicaciones(Nombre,CodigoCentro,Tipo,Activo) VALUES(N'Filial Santo Domingo',N'FILIAL-SANTO-DOMINGO',1,1);

MERGE dbo.ChoferesInternos AS destino
USING (VALUES
 (N'EMP-1001',N'Carlos Martínez',CAST(1 AS BIT)),
 (N'EMP-1002',N'José Rodríguez',CAST(1 AS BIT)),
 (N'EMP-0099',N'Miguel Pérez (inactivo)',CAST(0 AS BIT))
) AS origen(NumeroEmpleado,NombreCompleto,Activo)
ON destino.NumeroEmpleado=origen.NumeroEmpleado
WHEN MATCHED THEN UPDATE SET NombreCompleto=origen.NombreCompleto,Activo=origen.Activo
WHEN NOT MATCHED THEN INSERT(NombreCompleto,NumeroEmpleado,Activo,UsuarioCreacionId) VALUES(origen.NombreCompleto,origen.NumeroEmpleado,origen.Activo,@Usuario);

DECLARE @Filial INT=(SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro=N'FILIAL-PRINCIPAL');
DECLARE @Tecnologia INT=(SELECT UbicacionId FROM dbo.Ubicaciones WHERE CodigoCentro=N'TECNOLOGIA');
DECLARE @Laptop INT=(SELECT TipoEquipoId FROM dbo.TiposEquipo WHERE Nombre=N'Laptop');
DECLARE @Monitor INT=(SELECT TipoEquipoId FROM dbo.TiposEquipo WHERE Nombre=N'Monitor');
DECLARE @Interno INT=(SELECT TipoTransporteId FROM dbo.TiposTransporte WHERE Codigo=N'INTERNO');
DECLARE @Privado INT=(SELECT TipoTransporteId FROM dbo.TiposTransporte WHERE Codigo=N'PRIVADO');
DECLARE @Chofer1 INT=(SELECT ChoferInternoId FROM dbo.ChoferesInternos WHERE NumeroEmpleado=N'EMP-1001');
DECLARE @Chofer2 INT=(SELECT ChoferInternoId FROM dbo.ChoferesInternos WHERE NumeroEmpleado=N'EMP-1002');
DECLARE @Ahora DATETIME2(7)=SYSUTCDATETIME();

INSERT dbo.Equipos(CodigoActivo,NumeroSerie,TipoEquipoId,UbicacionActualId,Marca,Modelo,Observaciones,UsuarioCreacionId)
VALUES
(N'TEST-EQ-001',N'TEST-SN-001',@Laptop,@Filial,N'Dell',N'Latitude 5420',N'Equipo para flujo interno preparado.',@Usuario),
(N'TEST-EQ-002',N'TEST-SN-002',@Monitor,@Filial,N'Dell',N'P2422H',N'Equipo pendiente de confirmación por Transportación.',@Usuario),
(N'TEST-EQ-003',N'TEST-SN-003',@Laptop,@Filial,N'HP',N'ProBook 440',N'Equipo interno en tránsito.',@Usuario),
(N'TEST-EQ-004',N'TEST-SN-004',@Laptop,@Filial,N'Lenovo',N'ThinkPad E14',N'Equipo preparado para entrega privada.',@Usuario),
(N'TEST-EQ-005',N'TEST-SN-005',@Monitor,@Filial,N'HP',N'E24 G5',N'Equipo privado en tránsito.',@Usuario),
(N'TEST-EQ-006',N'TEST-SN-006',@Laptop,@Tecnologia,N'Dell',N'Latitude 5430',N'Equipo privado esperando Tecnología.',@Usuario);

INSERT dbo.Envios(NumeroEnvio,UbicacionOrigenId,UbicacionDestinoId,EstadoEnvioId,Direccion,UsuarioSolicitanteId,Observaciones,FechaCreacion,UsuarioCreacionId)
SELECT v.Numero,@Filial,@Tecnologia,e.EstadoEnvioId,1,@Usuario,v.Observacion,DATEADD(MINUTE,v.Minutos,@Ahora),@Usuario
FROM (VALUES
(N'TEST-INT-PREPARADO',N'Interno listo para usar el botón Entregado a transportación.',N'EN_FILIAL',-60),
(N'TEST-INT-CONFIRMACION',N'Interno visible en Confirmaciones de Transportación.',N'PENDIENTE_CONFIRMACION_TRANSPORTE',-50),
(N'TEST-INT-TRANSITO',N'Interno confirmado y actualmente en tránsito.',N'EN_TRANSITO',-40),
(N'TEST-PRI-PREPARADO',N'Privado listo para registrar la entrega.',N'EN_FILIAL',-30),
(N'TEST-PRI-TRANSITO',N'Privado entregado y en tránsito directo.',N'EN_TRANSITO',-20),
(N'TEST-PRI-TECNOLOGIA',N'Privado ya recibido y esperando Tecnología.',N'ESPERA_TECNOLOGIA',-10)
)v(Numero,Observacion,CodigoEstado,Minutos)
JOIN dbo.EstadosEnvio e ON e.Codigo=v.CodigoEstado;

INSERT dbo.EnvioEquipos(EnvioId,EquipoId,NumeroTicket,UsuarioSolicitanteId,Observaciones,UsuarioCreacionId)
SELECT en.EnvioId,eq.EquipoId,REPLACE(en.NumeroEnvio,N'TEST-',N'TKT-'),@Usuario,N'Asociación de prueba.',@Usuario
FROM (VALUES
(N'TEST-INT-PREPARADO',N'TEST-EQ-001'),(N'TEST-INT-CONFIRMACION',N'TEST-EQ-002'),(N'TEST-INT-TRANSITO',N'TEST-EQ-003'),
(N'TEST-PRI-PREPARADO',N'TEST-EQ-004'),(N'TEST-PRI-TRANSITO',N'TEST-EQ-005'),(N'TEST-PRI-TECNOLOGIA',N'TEST-EQ-006'))v(Numero,Codigo)
JOIN dbo.Envios en ON en.NumeroEnvio=v.Numero JOIN dbo.Equipos eq ON eq.CodigoActivo=v.Codigo;

INSERT dbo.ReservasEquipoEnvio(EquipoId,EnvioId,FechaReserva,UsuarioId)
SELECT EquipoId,EnvioId,@Ahora,@Usuario FROM dbo.EnvioEquipos ee WHERE EXISTS(SELECT 1 FROM dbo.Envios e WHERE e.EnvioId=ee.EnvioId AND e.NumeroEnvio LIKE N'TEST-%');

INSERT dbo.Transportes(EnvioId,TipoTransporteId,Observaciones,FechaCreacion,UsuarioCreacionId)
SELECT EnvioId,CASE WHEN NumeroEnvio LIKE N'TEST-INT-%' THEN @Interno ELSE @Privado END,N'Transporte de prueba.',FechaCreacion,@Usuario
FROM dbo.Envios WHERE NumeroEnvio LIKE N'TEST-%';

INSERT dbo.TransportesInternos(TransporteId,ChoferInternoId,NombreChoferAlMomento,NumeroEmpleadoAlMomento,FechaEntregaTransportacion,EntregaConfirmada,FechaConfirmacionEntrega,UsuarioConfirmacionId)
SELECT t.TransporteId,CASE WHEN e.NumeroEnvio=N'TEST-INT-TRANSITO' THEN @Chofer2 ELSE @Chofer1 END,
       CASE WHEN e.NumeroEnvio=N'TEST-INT-TRANSITO' THEN N'José Rodríguez' ELSE N'Carlos Martínez' END,
       CASE WHEN e.NumeroEnvio=N'TEST-INT-TRANSITO' THEN N'EMP-1002' ELSE N'EMP-1001' END,
       CASE WHEN e.NumeroEnvio=N'TEST-INT-PREPARADO' THEN NULL ELSE DATEADD(MINUTE,-20,@Ahora) END,
       CASE WHEN e.NumeroEnvio=N'TEST-INT-TRANSITO' THEN 1 ELSE 0 END,
       CASE WHEN e.NumeroEnvio=N'TEST-INT-TRANSITO' THEN DATEADD(MINUTE,-15,@Ahora) END,
       CASE WHEN e.NumeroEnvio=N'TEST-INT-TRANSITO' THEN @Usuario END
FROM dbo.Transportes t JOIN dbo.Envios e ON e.EnvioId=t.EnvioId WHERE e.NumeroEnvio LIKE N'TEST-INT-%';

INSERT dbo.TransportesPrivados(TransporteId,NombreResponsable,Parentesco,CedulaResponsable,PlacaVehiculo,FechaEntrega,UsuarioQueEntregoId)
SELECT t.TransporteId,v.Nombre,v.Parentesco,v.Cedula,v.Placa,
       CASE WHEN e.NumeroEnvio=N'TEST-PRI-PREPARADO' THEN NULL ELSE DATEADD(MINUTE,-15,@Ahora) END,
       CASE WHEN e.NumeroEnvio=N'TEST-PRI-PREPARADO' THEN NULL ELSE @Usuario END
FROM dbo.Transportes t JOIN dbo.Envios e ON e.EnvioId=t.EnvioId
JOIN (VALUES
(N'TEST-PRI-PREPARADO',N'Ana Gómez',N'Madre','00112345678',N'A123456'),
(N'TEST-PRI-TRANSITO',N'Luis Hernández',N'Padre','00123456789',N'B765432'),
(N'TEST-PRI-TECNOLOGIA',N'María Santos',N'Tutora','00134567890',N'C456789'))v(Numero,Nombre,Parentesco,Cedula,Placa) ON v.Numero=e.NumeroEnvio;

INSERT dbo.HistorialesEstadoEnvio(EnvioId,EstadoEnvioId,Fecha,UbicacionId,UsuarioId,Observaciones,UsuarioCreacionId)
SELECT e.EnvioId,e.EstadoEnvioId,e.FechaCreacion,
       CASE WHEN es.Codigo=N'ESPERA_TECNOLOGIA' THEN @Tecnologia ELSE @Filial END,@Usuario,N'Estado de escenario de prueba.',@Usuario
FROM dbo.Envios e JOIN dbo.EstadosEnvio es ON es.EstadoEnvioId=e.EstadoEnvioId WHERE e.NumeroEnvio LIKE N'TEST-%';

INSERT dbo.Notificaciones(EnvioId,Tipo,Titulo,Mensaje,DestinatarioRol,FechaCreacion)
SELECT EnvioId,N'ENVIO_DISPONIBLE_TECNOLOGIA',N'Envío disponible para retiro',N'El envío TEST-PRI-TECNOLOGIA está disponible para ser retirado de Transportación.',N'TECNOLOGIA',@Ahora
FROM dbo.Envios WHERE NumeroEnvio=N'TEST-PRI-TECNOLOGIA';

COMMIT TRANSACTION;
SELECT N'Datos de prueba cargados' Resultado,
 (SELECT COUNT(*) FROM dbo.Envios WHERE NumeroEnvio LIKE N'TEST-%') Envios,
 (SELECT COUNT(*) FROM dbo.Equipos WHERE CodigoActivo LIKE N'TEST-%') Equipos,
 (SELECT COUNT(*) FROM dbo.ChoferesInternos WHERE NumeroEmpleado LIKE N'EMP-%') Choferes;
GO
