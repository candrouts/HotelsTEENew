using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Καταγραφή ερωταπαντήσεων του AI Συμβούλου Βιωσιμότητας.
    [Table("TEE_AI_ChatLog")]
    public class AiChatLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public string userName { get; set; }

        public string question { get; set; }

        public string answer { get; set; }

        public DateTime logDateTime { get; set; }
    }
}
