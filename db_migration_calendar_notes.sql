-- ============================================================
--  Ημερολόγιο: προσωπικές σημειώσεις (to-do) χρήστη
--  Feature: home dashboard επιθεωρητή + σελίδα /Calendar
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_UserCalendarNotes')
BEGIN
    CREATE TABLE dbo.TEE_UserCalendarNotes
    (
        noteID            NUMERIC(10,0)  IDENTITY(1,1) NOT NULL,
        userName          NVARCHAR(256)  NOT NULL,
        noteDate          DATE           NOT NULL,
        title             NVARCHAR(250)  NOT NULL,
        color             NVARCHAR(20)   NULL,
        isDone            BIT            NOT NULL CONSTRAINT DF_UserCalNotes_isDone DEFAULT (0),
        creationDateTime  DATETIME       NOT NULL CONSTRAINT DF_UserCalNotes_created DEFAULT (GETDATE()),
        CONSTRAINT PK_TEE_UserCalendarNotes PRIMARY KEY CLUSTERED (noteID)
    );

    CREATE INDEX IX_TEE_UserCalendarNotes_user
        ON dbo.TEE_UserCalendarNotes (userName, noteDate);
END
GO
