-- ============================================================
-- Migration: Inspector Selection tables
-- ============================================================

-- Σύνδεση επιθεωρητών με γεωγραφικές περιοχές (ELSTATAreas)
-- levelID=3 → Περιφέρεια (καλύπτει όλες τις ΠΕ της)
-- levelID=4 → Περιφερειακή Ενότητα (συγκεκριμένη ΠΕ)
CREATE TABLE TEE_Inspector_Areas (
    id          INT IDENTITY(1,1) NOT NULL,
    inspectorID NUMERIC(5,0)      NOT NULL,
    kalID       NVARCHAR(10)      NOT NULL,
    levelID     INT               NOT NULL,  -- 3=Περιφέρεια, 4=ΠΕ
    CONSTRAINT PK_TEE_Inspector_Areas PRIMARY KEY (id),
    CONSTRAINT FK_TEE_Inspector_Areas_Inspector
        FOREIGN KEY (inspectorID) REFERENCES TEE_Inspectors(id),
    CONSTRAINT UQ_TEE_Inspector_Areas UNIQUE (inspectorID, kalID)
);

CREATE INDEX IX_TEE_Inspector_Areas_Inspector ON TEE_Inspector_Areas (inspectorID);
CREATE INDEX IX_TEE_Inspector_Areas_KalID     ON TEE_Inspector_Areas (kalID);

-- Αιτήσεις επιλογής επιθεωρητή από ξενοδόχο
-- status: 1=αναμονή, 2=αποδοχή, 3=απόρριψη
CREATE TABLE TEE_HotelCriteria_InspectorRequests (
    id               INT IDENTITY(1,1) NOT NULL,
    hotelCriteriaID  NUMERIC(18,0)     NOT NULL,
    inspectorID      NUMERIC(5,0)      NOT NULL,
    proposedDate     DATE              NULL,
    status           INT               NOT NULL DEFAULT 1,
    creationDateTime DATETIME          NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_TEE_HotelCriteria_InspectorRequests PRIMARY KEY (id),
    CONSTRAINT FK_TEE_HCI_Requests_HotelCriteria
        FOREIGN KEY (hotelCriteriaID) REFERENCES TEE_HotelCriteria(id),
    CONSTRAINT FK_TEE_HCI_Requests_Inspector
        FOREIGN KEY (inspectorID) REFERENCES TEE_Inspectors(id)
);

CREATE INDEX IX_TEE_HCI_Requests_HotelCriteria ON TEE_HotelCriteria_InspectorRequests (hotelCriteriaID);
