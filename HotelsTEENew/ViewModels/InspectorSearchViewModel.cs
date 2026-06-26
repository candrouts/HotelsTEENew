using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    public class InspectorSearchViewModel
    {
        public decimal id { get; set; }
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string taxNumber { get; set; }
        public string areas { get; set; }
    }

    public class ELSTATAreaViewModel
    {
        public string kalID { get; set; }
        public string title { get; set; }
        public int levelID { get; set; }
        public string parentID { get; set; }
    }

    public class InspectorFilterViewModel
    {
        public string perifereaKalID { get; set; }
        public string peKalID { get; set; }
        public string name { get; set; }
    }

    // Payload από το frontend για υποβολή αίτησης
    public class InspectorSubmitViewModel
    {
        public decimal hotelCriteriaID { get; set; }
        public decimal inspectorID { get; set; }
        public string proposedDate { get; set; }
    }

    // Πληροφορίες υπάρχοντος HotelierCertificate για την οθόνη
    public class ExistingCertificateInfoViewModel
    {
        public string inspectorName { get; set; }
        public string inspectorEmail { get; set; }
        public string inspectorPhone { get; set; }
        public string proposedDate { get; set; }
        public string submissionDate { get; set; }

        public decimal certificateID { get; set; }
        public int? autopsyDateStatus { get; set; }     // 1=εκκρεμεί, 2=αποδοχή, 3=οριστική με αλλαγή

        // Στάδια των εκδόσεων του κύκλου (null = δεν υπάρχει, 1=draft, 2=υποβλήθηκε)
        public int? v1Status { get; set; }
        public int? v2Status { get; set; }
        public int? v3Status { get; set; }

        // Αναμένεται αποδοχή τελικής κατάταξης από τον ξενοδόχο
        public bool awaitingAcceptance { get; set; }
        public bool isIssued { get; set; }              // εκδόθηκε βεβαίωση

        // Στοιχεία τελικής κατάταξης (όταν v3 υποβλήθηκε)
        public decimal? finalTotalPoints { get; set; }
        public string finalMedalTitle { get; set; }
    }

    public class TimelineStepViewModel
    {
        public string title { get; set; }
        public string subtitle { get; set; }
        public string date { get; set; }       // null = δεν έχει γίνει ακόμα
        public bool isCompleted { get; set; }
        public bool isPending { get; set; }      // σε εξέλιξη / προγραμματισμένο
        public string pendingLabel { get; set; } // λεκτικό badge όταν isPending (π.χ. "Σε εξέλιξη", "Προγραμματισμένη")
        public string icon { get; set; }
        public string colorClass { get; set; } // success, primary, warning, secondary
    }

    public class InspectorAreaSaveViewModel
    {
        public string kalID { get; set; }
        public int levelID { get; set; }
    }

    // Σχόλιο απόρριψης τελικής κατάταξης από ξενοδόχο
    public class RejectFinalViewModel
    {
        public string comment { get; set; }
    }

    // Αποθήκευση περιοχών επιθεωρητή από τον admin (inspectorID + λίστα περιοχών)
    public class AdminInspectorAreasSaveViewModel
    {
        public decimal inspectorID { get; set; }
        public List<InspectorAreaSaveViewModel> areas { get; set; }
    }

    public class InspectorInitialDataViewModel
    {
        public List<ELSTATAreaViewModel> perifereies { get; set; }
        public decimal? hotelCriteriaID { get; set; }
        public int hotelCriteriaStatus { get; set; }
        public ExistingCertificateInfoViewModel existingCertificate { get; set; }
        public List<TimelineStepViewModel> timeline { get; set; }

        // Η προηγούμενη ανάθεση απορρίφθηκε από τον επιθεωρητή → ο ξενοδόχος ξαναεπιλέγει
        public bool assignmentRejected { get; set; }
        public string rejectedInspectorName { get; set; }
        public string rejectionNote { get; set; }
    }
}
