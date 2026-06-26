using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_Criteria_Files")]
    public class Criteria_File
    {
        //id, criteriaID, title, isActive, description

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public decimal criteriaID { get; set; }

        public string title { get; set; }

        public bool isActive { get; set; }

        public string description { get; set; }

        [ForeignKey("criteriaID")]
        public Criteria criteria { get; set; }

    }
}