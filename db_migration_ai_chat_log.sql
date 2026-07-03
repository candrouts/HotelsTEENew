-- ============================================================
--  AI Σύμβουλος Βιωσιμότητας: καταγραφή συνομιλιών (ai branch)
--  Χρήσεις: feedback τι ρωτούν οι ξενοδόχοι + ημερήσιο όριο χρήσης.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_AI_ChatLog')
BEGIN
    CREATE TABLE dbo.TEE_AI_ChatLog
    (
        id            NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        userName      NVARCHAR(256) NOT NULL,
        question      NVARCHAR(2000) NULL,
        answer        NVARCHAR(MAX) NULL,
        logDateTime   DATETIME      NOT NULL CONSTRAINT DF_AIChatLog_dt DEFAULT (GETDATE()),
        CONSTRAINT PK_TEE_AI_ChatLog PRIMARY KEY CLUSTERED (id)
    );
    CREATE INDEX IX_TEE_AI_ChatLog_user ON dbo.TEE_AI_ChatLog (userName, logDateTime DESC);
END
GO
