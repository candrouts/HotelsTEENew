using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_CertificateTemplate")]
    public class CertificateTemplate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public string title { get; set; }

        public string body { get; set; }

        public bool isActive { get; set; }

        public DateTime lastModified { get; set; }
    }
}
