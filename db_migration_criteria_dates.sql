-- ============================================================
-- Migration: Ημερομηνίες στον πίνακα TEE_HotelCriteria
-- (αν δεν τα έχεις ήδη προσθέσει χειροκίνητα, τρέξε τα ALTER TABLE)
-- ============================================================

-- Τα πεδία έχουν ήδη προστεθεί χειροκίνητα στη βάση:
-- ALTER TABLE TEE_HotelCriteria ADD creationDatetime DATETIME NULL;
-- ALTER TABLE TEE_HotelCriteria ADD lastModificationDateTime DATETIME NULL;


-- ============================================================
-- Ενημέρωση του view V_TEE_HotelCriteria ώστε να επιστρέφει
-- τα νέα πεδία creationDatetime και lastModificationDateTime
-- ============================================================
-- ΣΗΜΑΝΤΙΚΟ: Αντικατέστησε τον ορισμό παρακάτω με τον υπάρχοντα ορισμό
-- του view σου και πρόσθεσε τα δύο πεδία στο SELECT.
--
-- Για να δεις τον τρέχοντα ορισμό:
--   SELECT OBJECT_DEFINITION(OBJECT_ID('V_TEE_HotelCriteria'))
--
-- Παράδειγμα (προσάρμοσε ανάλογα):
/*
ALTER VIEW V_TEE_HotelCriteria AS
SELECT
    hc.id,
    hc.hotelID,
    hc.exploitingCompanyID,
    hc.status,
    hc.version,
    hc.totalPoints,
    hc.maxPoints,
    hc.medalID,
    m.title AS medalTitle,
    hc.certificateID,
    hc.creationDatetime,               -- <-- νέο πεδίο
    hc.lastModificationDateTime        -- <-- νέο πεδίο
FROM TEE_HotelCriteria hc
LEFT JOIN TEE_Medals m ON m.id = hc.medalID
*/
