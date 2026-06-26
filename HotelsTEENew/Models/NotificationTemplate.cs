using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_NotificationTemplates")]
    public class NotificationTemplate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public string eventKey { get; set; }

        public bool isActive { get; set; }

        // 1=Ξενοδόχος, 2=Ανατεθειμένος Επιθεωρητής, 3=Admin, 4=Custom
        public int recipientType { get; set; }

        public string customEmail { get; set; }

        public string subject { get; set; }

        public string body { get; set; }
    }
}
