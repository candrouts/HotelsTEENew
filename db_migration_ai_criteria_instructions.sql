-- ============================================================
--  AI Φάση 2: Οδηγίες ελέγχου ανά κριτήριο (ai branch)
--  «Εκπαίδευση» του ελέγχου τεκμηρίων ανά κριτήριο από τον admin.
--  ΞΕΧΩΡΙΣΤΟΣ πίνακας — κανένα ALTER στο κοινό TEE_Criteria.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_AI_CriteriaInstructions')
BEGIN
    CREATE TABLE dbo.TEE_AI_CriteriaInstructions
    (
        id             NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        criteriaID     NUMERIC(10,0) NOT NULL,     -- λογικό FK προς TEE_Criteria.id (unique ανά κριτήριο)
        instructions   NVARCHAR(2000) NOT NULL,
        updatedBy      NVARCHAR(256) NULL,
        updatedDateTime DATETIME     NOT NULL CONSTRAINT DF_AICritInstr_dt DEFAULT (GETDATE()),
        CONSTRAINT PK_TEE_AI_CriteriaInstructions PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_TEE_AI_CriteriaInstructions_crit UNIQUE (criteriaID)
    );
END
GO
