-- ============================================================
-- Migration: Ελάχιστες βάσεις μεταλλίων ανά πυλώνα + ρυθμίσεις
-- + αποθήκευση αναγμένης βαθμολογίας (0..95) στο TEE_HotelCriteria
-- ============================================================

-- Ελάχιστη βάση ανά (μετάλιο, κύριο πυλώνα)
-- isPercent=1 → minValue ως % του max του πυλώνα (totalUnits)
-- isPercent=0 → minValue ως απόλυτος αριθμός (στην αναγμένη κλίμακα)
CREATE TABLE TEE_Medal_PillarThreshold (
    id         INT IDENTITY(1,1) NOT NULL,
    medalID    NUMERIC(18,0)     NOT NULL,
    categoryID NUMERIC(18,0)     NOT NULL,  -- κύριος πυλώνας (TEE_Categories, parentID IS NULL)
    minValue   DECIMAL(9,2)      NOT NULL DEFAULT 0,
    isPercent  BIT               NOT NULL DEFAULT 1,
    CONSTRAINT PK_TEE_Medal_PillarThreshold PRIMARY KEY (id),
    CONSTRAINT UQ_TEE_Medal_PillarThreshold UNIQUE (medalID, categoryID)
);

-- Γενικές ρυθμίσεις συστήματος (key/value)
CREATE TABLE TEE_Settings (
    settingKey   NVARCHAR(80)  NOT NULL,
    settingValue NVARCHAR(400) NULL,
    CONSTRAINT PK_TEE_Settings PRIMARY KEY (settingKey)
);

-- Διακόπτης: εφαρμογή ελάχιστων βάσεων ανά πυλώνα (0=όχι, 1=ναι)
INSERT INTO TEE_Settings (settingKey, settingValue) VALUES ('medal.usePillarThresholds', '0');

-- Αναγμένη συνολική βαθμολογία (0..95) — ώστε όλα τα σημεία να δείχνουν
-- το ίδιο νούμερο με τη μπάρα (το totalPoints παραμένει η raw βαθμολογία)
ALTER TABLE TEE_HotelCriteria ADD totalScore DECIMAL(9,2) NULL;

-- (προαιρετικό) και στο view V_TEE_HotelCriteria:
--   ALTER VIEW ... προσθήκη της στήλης totalScore
