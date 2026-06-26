using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_HotelCriteria")]
    public class HotelCriteria
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public string hotelID { get; set; }

        public string exploitingCompanyID { get; set; }

        public int status { get; set; }

        public int version { get; set; }

        public decimal totalPoints { get; set; }

        public decimal maxPoints { get; set; }

        public decimal? medalID { get; set; }

        [ForeignKey("medalID")]
        public Medal medal { get; set; }


        public decimal? certificateID { get; set; }

        [ForeignKey("certificateID")]
        public HotelierCertificate certificate { get; set; }

        public DateTime? creationDatetime { get; set; }

        public DateTime? lastModificationDateTime { get; set; }

        // true όταν ο κύκλος αξιολόγησης ολοκληρώθηκε (εκδόθηκε βεβαίωση)
        public bool isFinished { get; set; }

        // Αναγμένη συνολική βαθμολογία (0..95) — ίδιο νούμερο με τη μπάρα
        public decimal? totalScore { get; set; }

    }
}