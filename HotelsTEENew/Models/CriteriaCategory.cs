using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_Categories")]
    public class CriteriaCategory
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public string title { get; set; }

        public string description { get; set; }

        public string examples { get; set; }

        public int order { get; set; }

        public decimal? totalUnits { get; set; }

        public decimal? maxGrade { get; set; }

        public bool isActive { get; set; }

        public decimal? parentID { get; set; }

        [ForeignKey("parentID")]
        public CriteriaCategory parent { get; set; }


    }
}