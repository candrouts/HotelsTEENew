-- ============================================================
-- Migration: Έκδοση & αποθήκευση εγγράφου Βεβαίωσης + πρότυπο
-- ============================================================

-- Ο πίνακας CertificateFiles υπάρχει ήδη:
--   certificateFileID numeric(10,0)  (PK, IDENTITY)
--   certificateFile   varbinary(MAX)
--   title             nvarchar(550)
--   fileType          nvarchar(25)
--   creationDateTime  datetime

-- Σύνδεση HotelierCertificates -> CertificateFiles (αν δεν υπάρχει ήδη το πεδίο)
-- ALTER TABLE HotelierCertificates ADD certificateFileID NUMERIC(10,0) NULL;

-- Αύξων αριθμός βεβαίωσης (αν δεν υπάρχει ήδη το πεδίο certificationNumber).
-- ΠΡΟΣΟΧΗ: στην υπάρχουσα ΒΔ η στήλη είναι VARCHAR(20) (μοιραζόμενη με άλλους τύπους)
-- και για typeID=84 οι κενές τιμές είναι '' (όχι NULL). Ο κώδικας την αντιμετωπίζει ως string.
-- Χρησιμοποιείται για τον αριθμό βεβαίωσης: '4' + 5ψήφιος(certificationNumber) + έτος
-- ALTER TABLE HotelierCertificates ADD certificationNumber VARCHAR(20) NULL;

-- Στοιχεία ανάρτησης Διαύγειας (συμπληρώνονται στο go-live)
-- ALTER TABLE HotelierCertificates ADD ada NVARCHAR(60) NULL;
-- ALTER TABLE HotelierCertificates ADD diaugeiaDocumentUrl NVARCHAR(400) NULL;
-- ALTER TABLE HotelierCertificates ADD diaugeiaUrl NVARCHAR(400) NULL;

-- Πρότυπο (HTML) της βεβαίωσης — admin-editable
CREATE TABLE TEE_CertificateTemplate (
    id           INT IDENTITY(1,1) NOT NULL,
    title        NVARCHAR(200)     NULL,
    body         NVARCHAR(MAX)     NOT NULL DEFAULT '',
    isActive     BIT               NOT NULL DEFAULT 1,
    lastModified DATETIME          NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_TEE_CertificateTemplate PRIMARY KEY (id)
);
