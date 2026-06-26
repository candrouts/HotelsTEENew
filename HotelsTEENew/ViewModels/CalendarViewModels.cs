using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    // Ένα γεγονός στο ημερολόγιο (αυτοψία ή προσωπική σημείωση).
    public class CalendarEventViewModel
    {
        public string id { get; set; }          // "autopsy-123" ή "note-45"
        public string title { get; set; }
        public string start { get; set; }        // ISO date (yyyy-MM-dd)
        public string color { get; set; }
        public string type { get; set; }         // "autopsy" | "note"
        public bool editable { get; set; }       // σημειώσεις: true, αυτοψίες: false
        public bool isDone { get; set; }
        public string url { get; set; }          // για αυτοψίες: σύνδεσμος στην αίτηση
    }

    public class CalendarEventsResultViewModel
    {
        public bool success { get; set; }
        public List<CalendarEventViewModel> events { get; set; }
    }

    // Αίτημα αποθήκευσης/ενημέρωσης σημείωσης.
    public class CalendarNoteRequest
    {
        public decimal? noteID { get; set; }     // null = νέα
        public string noteDate { get; set; }     // yyyy-MM-dd
        public string title { get; set; }
        public string color { get; set; }
        public bool isDone { get; set; }
    }

    public class CalendarNoteResultViewModel
    {
        public bool success { get; set; }
        public decimal noteID { get; set; }
        public string message { get; set; }
    }
}
