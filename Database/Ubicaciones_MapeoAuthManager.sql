-- Mapeo de cada ubicacion con el id de filial que AuthManager emite en el claim "affiliate".
-- Idempotente: se puede volver a ejecutar sin efectos secundarios.
--
-- Casos que no coinciden por nombre:
--
--   * "Santo Domingo Este" lleva el 31, CONFIRMADO con el token real de su administrador.
--     Antes estaba en 16 porque se dedujo por eliminacion emparejandolo con "LOS MINA", y
--     esa deduccion era falsa: su administrador no podia entrar. Los ids que quedan sin
--     asignar (16, 32, 33, 38) corresponden a filiales de AuthManager que no estan en la
--     lista de ADR; no hay que forzarlos a ninguna.
--
--     Leccion para el proximo: un id deducido por eliminacion es una hipotesis. Solo se
--     confirma cuando entra un usuario real de esa filial.
--
--   * AuthManager 30 "SANTO DOMINGO" es el centro sede, donde esta Tecnologia. NO se usa
--     como llave de alcance: si Tecnologia llevara el 30, cualquier empleado de la sede con
--     perfil de filial veria todos los envios, porque todos pasan por Tecnologia.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE u SET u.FilialExternaId = m.FilialExternaId
FROM dbo.Ubicaciones u
JOIN (VALUES
    (N'AZUA',                      1),
    (N'BANI',                      2),
    (N'BARAHONA',                  3),
    (N'SANTO-DOMINGO-OESTE',       4),
    (N'BONAO',                     5),
    (N'CONSTANZA',                 6),
    (N'COTUI',                     7),
    (N'DAJABON',                   8),
    (N'EL-SEIBO',                  9),
    (N'GUERRA',                   10),
    (N'HATO-MAYOR',               11),
    (N'HIGUEY',                   12),
    (N'JARABACOA',                13),
    (N'LA-ROMANA',                14),
    (N'LA-VEGA',                  15),
    (N'SANTO-DOMINGO-ESTE',       31),   -- Confirmado con el token real de su administrador.
    (N'MONTECRISTI',              17),
    (N'NAGUA',                    18),
    (N'PUERTO-PLATA',             19),
    (N'SALCEDO',                  20),
    (N'SAN-CRISTOBAL',            21),
    (N'SAN-FRANCISCO-DE-MACORIS', 22),
    (N'SAN-JOSE-DE-OCOA',         23),
    (N'SAN-JUAN-DE-LA-MAGUANA',   24),   -- AuthManager: "SAN JUAN"
    (N'SAN-PEDRO-DE-MACORIS',     25),   -- AuthManager: "SAN PEDRO"
    (N'SANCHEZ',                  26),
    (N'SANTIAGO',                 27),
    (N'SOSUA',                    28),
    (N'MAO',                      29),   -- AuthManager: "VALVERDE MAO"
    (N'LAS-MATAS-DE-FARFAN',      34),   -- AuthManager: "LAS MATAS"
    (N'LUPERON',                  35),
    (N'RANCHO-ARRIBA',            36),
    (N'HAINA',                    37),
    (N'MAIMON',                   39)
) AS m (CodigoCentro, FilialExternaId) ON m.CodigoCentro = u.CodigoCentro;

COMMIT TRANSACTION;
GO

SELECT Nombre, CodigoCentro, FilialExternaId,
       CASE WHEN Tipo = 2 THEN 'Tecnologia (sin id por diseno)'
            WHEN FilialExternaId IS NULL THEN 'FALTA MAPEAR'
            ELSE 'ok' END AS Estado
FROM dbo.Ubicaciones ORDER BY Tipo DESC, Nombre;
GO
