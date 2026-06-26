using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    public class UpcomingAutopsyViewModel
    {
        public decimal certificateID { get; set; }
        public string hotelTitle { get; set; }
        public string area { get; set; }
        public string autopsyDate { get; set; }
        public int daysUntil { get; set; }   // <0 = εκπρόθεσμη, 0 = σήμερα
        public bool overdue { get; set; }
        public bool isToday { get; set; }
        public bool pendingConfirm { get; set; }  // autopsyDateStatus=1: πρώτα επιβεβαίωση ημ/νίας
    }

    public class InspectorDashboardViewModel
    {
        public bool success { get; set; }

        public int totalActive { get; set; }
        public int countNew { get; set; }
        public int countScheduled { get; set; }
        public int countAutopsyDue { get; set; }
        public int countAutopsyInProgress { get; set; }
        public int countFinal { get; set; }
        public int countAwaitingAcceptance { get; set; }
        public int countCompleted { get; set; }

        public List<UpcomingAutopsyViewModel> upcoming { get; set; }
    }
}
