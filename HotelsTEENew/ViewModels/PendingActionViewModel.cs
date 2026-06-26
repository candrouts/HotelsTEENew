using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    // Εκκρεμής ενέργεια χρήστη (computed από το state — δεν αποθηκεύεται)
    public class PendingActionViewModel
    {
        public string title { get; set; }
        public string description { get; set; }
        public string link { get; set; }
        public string icon { get; set; }        // π.χ. "mdi mdi-seal"
        public string colorClass { get; set; }  // primary, warning, danger, info
    }

    public class PendingActionsResultViewModel
    {
        public bool success { get; set; }
        public List<PendingActionViewModel> actions { get; set; }
    }
}
