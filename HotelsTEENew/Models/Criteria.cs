using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_Criteria")]
    public class Criteria
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public string title { get; set; }

        public string description { get; set; }

        public decimal categoryID { get; set; }

        public int order { get; set; }

        public string code { get; set; }

        public int weight { get; set; }

        public decimal maxGrade { get; set; }

        public int criteriaType { get; set; }

        public string gradesList { get; set; }

        public string notes1 { get; set; }

        public string notes2 { get; set; }

        public DateTime dateFrom { get; set; }

        public DateTime dateTo { get; set; }

        [ForeignKey("categoryID")]
        public CriteriaCategory category { get; set; }


        public string gradesOptions { get; set; }

        public bool? needsFiles { get; set; }


        public string selectList { get; set; }

        public bool? notApplicable { get; set; }

    }
}