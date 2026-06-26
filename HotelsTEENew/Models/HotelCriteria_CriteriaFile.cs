using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_HotelCriteria_CriteriaFiles")]
    public class HotelCriteria_CriteriaFile
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public decimal hotelCriteriaID { get; set; }

        public decimal criteriaFileID { get; set; }

        public string fileName { get; set; }

        public string fileType { get; set; }

        public DateTime creationDateTime { get; set; }


        [ForeignKey("hotelCriteriaID")]
        public HotelCriteria hotelCriteria { get; set; }

        [ForeignKey("criteriaFileID")]
        public Criteria_File criteriaFile { get; set; }



    }
}