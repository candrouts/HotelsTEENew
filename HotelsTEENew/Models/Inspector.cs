using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_Inspectors")]
    public class Inspector
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public string firstName { get; set; }

        public string lastName { get; set; }

        public string taxNumber { get; set; }

        public string email { get; set; }

        public string phone { get; set; }

        public bool isActive { get; set; }


    }
}