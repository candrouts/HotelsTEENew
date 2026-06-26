using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class UpdateAutopsyDateViewModel
    {
        public decimal certificateID { get; set; }

        public string autopsyDateTime { get; set; }
    }

    // Απόρριψη ανάθεσης από επιθεωρητή (με σχόλιο)
    public class RejectAssignmentViewModel
    {
        public decimal certificateID { get; set; }
        public string comment { get; set; }
    }
}