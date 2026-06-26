-- ============================================================
-- Migration: Δυναμικές Ειδοποιήσεις (templates + log)
-- Ο κατάλογος γεγονότων (events) ορίζεται στον κώδικα
-- (Utils/NotificationEvents.cs). Εδώ μόνο τα admin-editable templates
-- και το ιστορικό αποστολών.
-- ============================================================

CREATE TABLE TEE_NotificationTemplates (
    id            INT IDENTITY(1,1) NOT NULL,
    eventKey      NVARCHAR(60)      NOT NULL,   -- αντιστοιχεί σε event του catalog
    isActive      BIT               NOT NULL DEFAULT 1,
    recipientType INT               NOT NULL DEFAULT 1,  -- 1=Ξενοδόχος, 2=Επιθεωρητής, 3=Admin, 4=Custom
    customEmail   NVARCHAR(200)     NULL,
    subject       NVARCHAR(300)     NOT NULL DEFAULT '',
    body          NVARCHAR(MAX)     NOT NULL DEFAULT '',
    CONSTRAINT PK_TEE_NotificationTemplates PRIMARY KEY (id),
    CONSTRAINT UQ_TEE_NotificationTemplates_event UNIQUE (eventKey)
);

CREATE TABLE TEE_NotificationLog (
    id            INT IDENTITY(1,1) NOT NULL,
    eventKey      NVARCHAR(60)      NOT NULL,
    toEmail       NVARCHAR(200)     NULL,
    subject       NVARCHAR(300)     NULL,
    sentDateTime  DATETIME          NOT NULL DEFAULT GETDATE(),
    success       BIT               NOT NULL DEFAULT 0,
    error         NVARCHAR(MAX)     NULL,
    CONSTRAINT PK_TEE_NotificationLog PRIMARY KEY (id)
);

CREATE INDEX IX_TEE_NotificationLog_event ON TEE_NotificationLog (eventKey, sentDateTime DESC);
