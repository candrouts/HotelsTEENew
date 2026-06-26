using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Προσωπικές σημειώσεις χρήστη πάνω στο ημερολόγιο (to-do).
    // Generic — δεν είναι δεμένο με ρόλο.
    [Table("TEE_UserCalendarNotes")]
    public class UserCalendarNote
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal noteID { get; set; }

        public string userName { get; set; }

        public DateTime noteDate { get; set; }

        public string title { get; set; }

        public string color { get; set; }

        public bool isDone { get; set; }

        public DateTime creationDateTime { get; set; }
    }
}
