using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_HotelCriteria_InspectorRequests")]
    public class HotelCriteriaInspectorRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public decimal hotelCriteriaID { get; set; }

        [ForeignKey("hotelCriteriaID")]
        public HotelCriteria hotelCriteria { get; set; }

        public decimal inspectorID { get; set; }

        [ForeignKey("inspectorID")]
        public Inspector inspector { get; set; }

        public DateTime? proposedDate { get; set; }

        // 1=αναμονή, 2=αποδοχή, 3=απόρριψη
        public int status { get; set; }

        public DateTime creationDateTime { get; set; }
    }
}
