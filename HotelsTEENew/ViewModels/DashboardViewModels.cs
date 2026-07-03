using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    // Φίλτρα από το frontend
    public class DashboardFilterViewModel
    {
        public string hotelCategory { get; set; }   // κατηγορία καταλύματος (π.χ. 5*)
        public string periphereiaID { get; set; }   // kalID Περιφέρειας
        public string peripheryID { get; set; }     // kalID Περιφερειακής Ενότητας
        public int? bedsFrom { get; set; }          // κλίμακα κλινών
        public int? bedsTo { get; set; }

        // Στάδιο σύγκρισης: 1 = Αυτοαξιολόγηση, 3 = Τελική Κατάταξη.
        // null = αυτόματη επιλογή (το πιο ώριμο στάδιο του ξενοδόχου)
        public int? version { get; set; }

        // Μόνο για admin: προβολή των charts "ως ξενοδόχος" για συγκεκριμένο ξενοδοχείο
        public string targetHotelID { get; set; }
        public string targetCompanyID { get; set; }
    }

    // Γραμμή SQL: άθροισμα πόντων + μέγιστο ανά (αξιολόγηση, υποκατηγορία)
    public class DashboardScoreRowViewModel
    {
        public decimal hotelCriteriaID { get; set; }
        public decimal categoryID { get; set; }
        public decimal points { get; set; }
        public decimal maxPoints { get; set; }   // μέγιστο βάσει εφαρμόσιμων κριτηρίων
    }

    // Στοιχείο chart (κατηγορία ή υποκατηγορία)
    public class DashboardItemViewModel
    {
        public decimal id { get; set; }
        public decimal? parentID { get; set; }   // null για κύριες κατηγορίες
        public string title { get; set; }
        public decimal myPoints { get; set; }    // κατηγορίες: αναγμένη τιμή | υποκατηγορίες: πόντοι
        public decimal avgPoints { get; set; }

        // Μόνο για κύριες κατηγορίες: οι τιμές ΠΡΙΝ την αναγωγή
        public decimal myRawPoints { get; set; }
        public decimal avgRawPoints { get; set; }
    }

    // Επιλογή φίλτρου (value/label)
    public class DashboardFilterOptionViewModel
    {
        public string value { get; set; }
        public string title { get; set; }
    }

    // Στοιχείο ιστορικού ολοκληρωμένων βεβαιώσεων (αρχική ξενοδόχου)
    public class CertificateHistoryItemViewModel
    {
        public decimal certificateID { get; set; }
        public string issueDate { get; set; }
        public string validUntil { get; set; }
        public bool isValid { get; set; }            // εντός 3ετίας
        public decimal totalPoints { get; set; }
        public string medalTitle { get; set; }
        public bool hasV1 { get; set; }
        public bool hasV2 { get; set; }
        public bool hasV3 { get; set; }
        public bool hasFile { get; set; }   // έχει παραχθεί το έγγραφο βεβαίωσης
    }

    public class CertificateHistoryViewModel
    {
        public bool success { get; set; }
        public bool hasActiveCycle { get; set; }     // υπάρχει ενεργή (μη ολοκληρωμένη) διαδικασία
        public bool aiEnabled { get; set; }          // διαθεσιμότητα AI λειτουργιών (ai branch)
        public List<CertificateHistoryItemViewModel> history { get; set; }
    }

    public class DashboardViewModel
    {
        public bool isHotelier { get; set; }
        public bool isAdmin { get; set; }                 // role=100: charts μόνο με Μ.Ο. (χωρίς "δική μου" βαθμολογία)
        public bool viewAsHotel { get; set; }             // admin με επιλεγμένο ξενοδοχείο → charts όπως ο ξενοδόχος
        public string targetHotelTitle { get; set; }      // τίτλος επιλεγμένου ξενοδοχείου
        public bool hasSubmitted { get; set; }            // έχει υποβληθεί αυτοαξιολόγηση (v1 s2)
        public int othersCount { get; set; }              // πλήθος ξενοδοχείων στον Μ.Ο.

        public int stage { get; set; }                    // το στάδιο που τελικά χρησιμοποιήθηκε (1 ή 3)
        public bool hasFinal { get; set; }                // υπάρχει δική του τελική κατάταξη (v3 s2)

        public List<DashboardItemViewModel> categories { get; set; }
        public List<DashboardItemViewModel> subCategories { get; set; }

        public List<DashboardFilterOptionViewModel> hotelCategories { get; set; }
        public List<DashboardFilterOptionViewModel> peripheries { get; set; }            // Περιφέρειες
        public List<DashboardFilterOptionViewModel> periferiakesEnotites { get; set; }   // Περιφερειακές Ενότητες
    }
}
