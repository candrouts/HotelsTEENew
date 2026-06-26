using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Αντιστοίχιση κεντρικής ρύθμισης → κριτήριο.
    // disableWhenPresent=1: το κριτήριο απενεργοποιείται όταν το feature ΥΠΑΡΧΕΙ.
    // disableWhenPresent=0: απενεργοποιείται όταν το feature ΛΕΙΠΕΙ (η συνήθης περίπτωση).
    [Table("TEE_FeatureCriteriaMap")]
    public class FeatureCriteriaMap
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal mapID { get; set; }

        public decimal featureID { get; set; }
        public decimal criteriaID { get; set; }
        public bool disableWhenPresent { get; set; }
    }
}
