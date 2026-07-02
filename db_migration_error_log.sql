-- ============================================================
--  Κεντρική καταγραφή σφαλμάτων εφαρμογής (TEE_ErrorLog)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_ErrorLog')
BEGIN
    CREATE TABLE dbo.TEE_ErrorLog
    (
        errorID          NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        logDateTime      DATETIME      NOT NULL CONSTRAINT DF_ErrorLog_dt DEFAULT (GETDATE()),
        source           NVARCHAR(300) NULL,     -- Controller.Action ή σημείο κώδικα
        userName         NVARCHAR(256) NULL,
        message          NVARCHAR(2000) NULL,
        stackTrace       NVARCHAR(MAX) NULL,
        CONSTRAINT PK_TEE_ErrorLog PRIMARY KEY CLUSTERED (errorID)
    );
    CREATE INDEX IX_TEE_ErrorLog_dt ON dbo.TEE_ErrorLog (logDateTime DESC);
END
GO
