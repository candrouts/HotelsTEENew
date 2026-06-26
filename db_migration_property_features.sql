-- ============================================================
--  Κεντρικές ρυθμίσεις/παροχές καταλύματος (data-driven)
--  → αυτόματη απενεργοποίηση κριτηρίων με notApplicable=1
--
--  Μοντέλο: κάθε feature είναι boolean (έχει/δεν έχει). Κάθε
--  αντιστοίχιση ορίζει αν το κριτήριο απενεργοποιείται όταν το
--  feature ΥΠΑΡΧΕΙ (disableWhenPresent=1) ή ΛΕΙΠΕΙ (=0).
--  Έτσι καλύπτονται και τα αμοιβαία αποκλειόμενα (π.χ. αποχέτευση).
-- ============================================================

-- 1) Οι κεντρικές ρυθμίσεις (διαχειρίζεται ο admin)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_PropertyFeature')
BEGIN
    CREATE TABLE dbo.TEE_PropertyFeature
    (
        featureID     NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        title         NVARCHAR(200) NOT NULL,
        description   NVARCHAR(500) NULL,
        icon          NVARCHAR(50)  NULL,
        displayOrder  INT           NOT NULL CONSTRAINT DF_PropFeature_order  DEFAULT (0),
        isActive      BIT           NOT NULL CONSTRAINT DF_PropFeature_active DEFAULT (1),
        CONSTRAINT PK_TEE_PropertyFeature PRIMARY KEY CLUSTERED (featureID)
    );
END
GO

-- 2) Αντιστοιχίσεις feature → κριτήριο (+ κατεύθυνση απενεργοποίησης)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_FeatureCriteriaMap')
BEGIN
    CREATE TABLE dbo.TEE_FeatureCriteriaMap
    (
        mapID               NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        featureID           NUMERIC(10,0) NOT NULL,
        criteriaID          NUMERIC(10,0) NOT NULL,
        disableWhenPresent  BIT           NOT NULL CONSTRAINT DF_FeatMap_present DEFAULT (0),
        CONSTRAINT PK_TEE_FeatureCriteriaMap PRIMARY KEY CLUSTERED (mapID)
    );
    CREATE INDEX IX_TEE_FeatureCriteriaMap_feature ON dbo.TEE_FeatureCriteriaMap (featureID);
    CREATE INDEX IX_TEE_FeatureCriteriaMap_criteria ON dbo.TEE_FeatureCriteriaMap (criteriaID);
END
GO

-- 3) Απαντήσεις ανά κύκλο/version (v1 ξενοδόχου, v2 επιθεωρητή)
--    Δένει με το συγκεκριμένο HotelCriteria (που φέρει version & certificateID).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TEE_HotelCriteria_Feature')
BEGIN
    CREATE TABLE dbo.TEE_HotelCriteria_Feature
    (
        id              NUMERIC(10,0) IDENTITY(1,1) NOT NULL,
        hotelCriteriaID NUMERIC(18,0) NOT NULL,
        featureID       NUMERIC(10,0) NOT NULL,
        hasFeature      BIT           NOT NULL CONSTRAINT DF_HCFeature_has DEFAULT (0),
        CONSTRAINT PK_TEE_HotelCriteria_Feature PRIMARY KEY CLUSTERED (id)
    );
    CREATE INDEX IX_TEE_HotelCriteria_Feature_hc ON dbo.TEE_HotelCriteria_Feature (hotelCriteriaID);
END
GO
