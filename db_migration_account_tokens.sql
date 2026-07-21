-- Εγγραφή/ενεργοποίηση λογαριασμών επιθεωρητών & επαναφορά κωδικού
-- Additive-only: νέος πίνακας, καμία αλλαγή σε υπάρχοντα αντικείμενα.
IF OBJECT_ID('TEE_AccountTokens') IS NULL
BEGIN
    CREATE TABLE TEE_AccountTokens (
        id INT IDENTITY(1,1) PRIMARY KEY,
        email NVARCHAR(256) NOT NULL,
        inspectorID NUMERIC(18,0) NULL,
        purpose NVARCHAR(20) NOT NULL,          -- 'register' | 'reset'
        tokenHash NVARCHAR(64) NOT NULL,        -- SHA256 hex του token (το token δεν αποθηκεύεται)
        expiresAt DATETIME NOT NULL,
        usedAt DATETIME NULL,
        createdIP NVARCHAR(45) NULL,
        createdDateTime DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_TEE_AccountTokens_tokenHash ON TEE_AccountTokens(tokenHash);
    CREATE INDEX IX_TEE_AccountTokens_email ON TEE_AccountTokens(email, createdDateTime);
END
