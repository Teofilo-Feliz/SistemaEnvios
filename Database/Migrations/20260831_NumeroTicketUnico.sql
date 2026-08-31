SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS (
    SELECT NumeroTicket
    FROM dbo.EnvioEquipos
    GROUP BY NumeroTicket
    HAVING COUNT(*) > 1
)
    THROW 51000, 'Existen números de ticket duplicados. Corríjalos antes de crear el índice único.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.EnvioEquipos')
      AND name = N'UX_EnvioEquipos_NumeroTicket'
)
    CREATE UNIQUE INDEX UX_EnvioEquipos_NumeroTicket
        ON dbo.EnvioEquipos(NumeroTicket);

COMMIT TRANSACTION;
