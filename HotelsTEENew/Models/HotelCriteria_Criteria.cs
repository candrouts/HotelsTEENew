using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_HotelCriteria_Criteria")]
    public class HotelCriteria_Criteria
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public decimal hotelCriteriaID { get; set; }

        public decimal criteriaID { get; set; }

        public bool? isChecked { get; set; }

        public bool? isNotChecked { get; set; }

        public string value { get; set; }

        public decimal? points { get; set; }

        public bool isApplicable { get; set; }

        [ForeignKey("hotelCriteriaID")]
        public HotelCriteria hotelCriteria { get; set; }

        [ForeignKey("criteriaID")]
        public Criteria criteria { get; set; }

    }
}