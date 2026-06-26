using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class CertificateViewModel
    {

        
        public decimal certificateID { get; set; }
        public string UserName { get; set; }
        public string hotelID { get; set; }
        public string exploitingCompanyID { get; set; }
        public string hotelTitle { get; set; }
        public string certificateStatusTitle { get; set; }


        public int totalBeds { get; set; }

        public int totalRooms { get; set; }

        public string exploitingCompanyName { get; set; }
        public string taxNumber { get; set; }
        public string category { get; set; }
        public string municipalityTitle { get; set; }

        public string address { get; set; }
        public string zipCode { get; set; }

        public string phoneNumber1 { get; set; }
        public string email { get; set; }

        public string autopsyDateTime { get; set; }

        // 1=προτάθηκε από ξενοδόχο (εκκρεμεί), 2=αποδοχή, 3=οριστική με αλλαγή
        public int? autopsyDateStatus { get; set; }

        public DateTime? autopsyDateConfirmationDateTime { get; set; }

        // ── Workflow state (υπολογίζονται στον controller, όχι από view) ──
        public int? v2Status { get; set; }       // null=δεν ξεκίνησε, 1=draft, 2=υποβλήθηκε
        public int? v3Status { get; set; }       // null=δεν ξεκίνησε, 1=draft, 2=υποβλήθηκε
        public bool canDoAutopsy { get; set; }   // έφτασε η ημ/νία ή υπάρχει ήδη v2
        public bool canDoFinal { get; set; }     // v2 υποβλήθηκε οριστικά
        public bool isNew { get; set; }          // νέα ανάθεση
        public bool isAutopsyDue { get; set; }   // ήρθε η ημ/νία αυτοψίας, εκκρεμεί

        public bool isIssued { get; set; }       // εκδόθηκε βεβαίωση (certificateStatusID=2)
        public string rejectionNote { get; set; }// σχόλια/απορρίψεις από ξενοδόχο (certificate.notes)

        public decimal? medalID { get; set; }     // μετάλλιο τελικής κατάταξης (v3)
        public string medalTitle { get; set; }

        public string periphereiaTitle { get; set; }  // Περιφέρεια (admin φίλτρο)
        public string peripheryTitle { get; set; }    // Περιφερειακή Ενότητα (admin φίλτρο)

    }
}