using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Απάντηση κεντρικής ρύθμισης ανά κύκλο/version (δένει με το HotelCriteria).
    [Table("TEE_HotelCriteria_Feature")]
    public class HotelCriteriaFeature
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public decimal hotelCriteriaID { get; set; }
        public decimal featureID { get; set; }
        public bool hasFeature { get; set; }
    }
}
