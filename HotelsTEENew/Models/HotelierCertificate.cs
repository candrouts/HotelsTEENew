using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("HotelierCertificates")]
    public class HotelierCertificate
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal certificateID { get; set; }

        //[Required]
      
        public string hotelID { get; set; }
     
        public decimal certificateTypeID { get; set; }
     
        public decimal certificateStatusID { get; set; }
       
        public string exploitingCompanyID { get; set; }


        public DateTime creationDateTime { get; set; }

        public DateTime? issueDateTime { get; set; }

        public bool sendViaFax { get; set; }

        public bool sendViaEmail { get; set; }

        public string emailTo { get; set; }

        public string faxTo { get; set; }

        public string formCompanyName { get; set; }

        public string formSignNumber { get; set; }

        //  public string formReason { get; set; }

        public string formNewLegalName { get; set; }

        public string formMainBody { get; set; }

        public DateTime? formEndDate { get; set; }

        public string formChangeDynamic { get; set; }


        public string formNewLegalForm { get; set; }

        public string formHotelArea { get; set; }

        public string formExploitingDates { get; set; }

        public bool isFromProtocol { get; set; }

        public bool isArchived { get; set; }

        public decimal? amount { get; set; }

        public int incomingProtocolNumber { get; set; }

        public int outcomingProtocolNumber { get; set; }

        public int outcomingProtocolNumber2 { get; set; }



        public string notes { get; set; }


        public string paymentCode { get; set; }

        public decimal? stampRate { get; set; }

        public bool? isNAEcharged { get; set; }
        public bool isFromTitleApplication { get; set; }


        public decimal responsibleUserID { get; set; }


        public int? application_id { get; set; }

        public int? validityMonths { get; set; }

        public DateTime? validityStartDateTime { get; set; }
        public DateTime? validityStopDateTime { get; set; }


        public string createdExploitingCompanyID { get; set; }

    

      

        public DateTime? autopsyDateTime { get; set; }

        // Κατάσταση ημ/νίας αυτοψίας:
        // 1 = προτάθηκε από ξενοδόχο (εκκρεμεί απάντηση επιθεωρητή)
        // 2 = αποδοχή από επιθεωρητή
        // 3 = οριστική με αλλαγή ημ/νίας από επιθεωρητή
        public int? autopsyDateStatus { get; set; }

        // Πότε απάντησε ο επιθεωρητής (αποδοχή ή αλλαγή)
        public DateTime? autopsyDateConfirmationDateTime { get; set; }

        public string paymentCodeForAuditorCompany { get; set; }


    

        public bool? isUpgrade { get; set; }


        public decimal? tee_inspectorID { get; set; }

        [ForeignKey("tee_inspectorID")]
        public Inspector inspector { get; set; }

        // Σύνδεση με το παραγόμενο έγγραφο βεβαίωσης (CertificateFiles)
        public decimal? certificateFileID { get; set; }

        // Αύξων αριθμός βεβαίωσης (ανά certificateTypeID) — βάση για τον αριθμό βεβαίωσης.
        // Η στήλη στη ΒΔ είναι varchar(20) (μοιραζόμενη με άλλους τύπους), γι' αυτό string.
        public string certificationNumber { get; set; }

        // Στοιχεία ανάρτησης στη Διαύγεια (συμπληρώνονται κατά την ανάρτηση)
        public string ada { get; set; }
        public string diaugeiaDocumentUrl { get; set; }
        public string diaugeiaUrl { get; set; }

    }
}