using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Κεντρική ρύθμιση/παροχή καταλύματος (έχει/δεν έχει).
    [Table("TEE_PropertyFeature")]
    public class PropertyFeature
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal featureID { get; set; }

        public string title { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
        public int displayOrder { get; set; }
        public bool isActive { get; set; }
    }
}
