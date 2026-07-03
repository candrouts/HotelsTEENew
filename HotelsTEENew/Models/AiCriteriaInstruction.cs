using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Οδηγίες AI ελέγχου τεκμηρίων ανά κριτήριο (μία εγγραφή ανά κριτήριο).
    [Table("TEE_AI_CriteriaInstructions")]
    public class AiCriteriaInstruction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public decimal criteriaID { get; set; }

        public string instructions { get; set; }

        public string updatedBy { get; set; }

        public DateTime updatedDateTime { get; set; }
    }
}
