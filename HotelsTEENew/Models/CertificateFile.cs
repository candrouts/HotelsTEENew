using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("CertificateFiles")]
    public class CertificateFile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal certificateFileID { get; set; }

        public byte[] certificateFile { get; set; }

        public string title { get; set; }

        public string fileType { get; set; }

        public DateTime creationDateTime { get; set; }
    }
}
