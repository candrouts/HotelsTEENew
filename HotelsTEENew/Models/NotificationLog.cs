using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_NotificationLog")]
    public class NotificationLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public string eventKey { get; set; }

        public string toEmail { get; set; }

        public string subject { get; set; }

        public DateTime sentDateTime { get; set; }

        public bool success { get; set; }

        public string error { get; set; }
    }
}
