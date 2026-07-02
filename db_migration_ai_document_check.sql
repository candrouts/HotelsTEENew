-- ============================================================
--  AI Προ-έλεγχος Τεκμηρίων (ai branch / greencertai)
--  Αποτελέσματα ελέγχου ανά μεταφορτωμένο τεκμήριο.
--  ADDITIVE-ONLY: νέος πίνακας, καμία αλλαγή σε υπάρχοντες.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_AI_DocumentCheck')
BEGIN
    CREATE TABLE dbo.TEE_AI_DocumentCheck
    (
        checkID              NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        hotelCriteriaFileID  NUMERIC(18,0) NOT NULL,   -- FK λογικό προς TEE_HotelCriteria_CriteriaFiles.id
        verdict              NVARCHAR(10)  NOT NULL,   -- ok | warn | fail
        summary              NVARCHAR(1500) NULL,      -- αιτιολόγηση του μοντέλου
        model                NVARCHAR(50)  NULL,
        checkedBy            NVARCHAR(256) NULL,
        checkedDateTime      DATETIME      NOT NULL CONSTRAINT DF_AIDocCheck_dt DEFAULT (GETDATE()),
        CONSTRAINT PK_TEE_AI_DocumentCheck PRIMARY KEY CLUSTERED (checkID)
    );
    CREATE INDEX IX_TEE_AI_DocumentCheck_file ON dbo.TEE_AI_DocumentCheck (hotelCriteriaFileID, checkedDateTime DESC);
END
GO
