using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Cache μιας AI ανάλυσης θεμάτων chat (AI Insights admin).
    [Table("TEE_AI_InsightsReport")]
    public class AiInsightsReport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public int periodDays { get; set; }

        public string reportText { get; set; }

        public string createdBy { get; set; }

        public DateTime createdDateTime { get; set; }
    }
}
