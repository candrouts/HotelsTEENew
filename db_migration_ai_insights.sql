-- ============================================================
--  AI Insights (admin): cache των AI αναλύσεων θεμάτων chat
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_AI_InsightsReport')
BEGIN
    CREATE TABLE dbo.TEE_AI_InsightsReport
    (
        id              NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        periodDays      INT           NOT NULL,
        reportText      NVARCHAR(MAX) NOT NULL,
        createdBy       NVARCHAR(256) NULL,
        createdDateTime DATETIME      NOT NULL CONSTRAINT DF_AIInsights_dt DEFAULT (GETDATE()),
        CONSTRAINT PK_TEE_AI_InsightsReport PRIMARY KEY CLUSTERED (id)
    );
END
GO
