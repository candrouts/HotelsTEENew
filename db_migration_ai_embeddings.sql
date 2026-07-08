-- ============================================================
--  AI Semantic Search: cache embeddings κριτηρίων (ai branch)
--  Ένα embedding ανά κριτήριο (title+description), με hash για
--  αυτόματη ανανέωση όταν αλλάζει το περιεχόμενο.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_AI_CriteriaEmbedding')
BEGIN
    CREATE TABLE dbo.TEE_AI_CriteriaEmbedding
    (
        id              NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        criteriaID      NUMERIC(10,0) NOT NULL,
        contentHash     NVARCHAR(50)  NOT NULL,     -- MD5(title+description)
        embedding       NVARCHAR(MAX) NOT NULL,     -- JSON array float
        updatedDateTime DATETIME      NOT NULL CONSTRAINT DF_AIEmb_dt DEFAULT (GETDATE()),
        CONSTRAINT PK_TEE_AI_CriteriaEmbedding PRIMARY KEY CLUSTERED (id),
        CONSTRAINT UQ_TEE_AI_CriteriaEmbedding_crit UNIQUE (criteriaID)
    );
END
GO
