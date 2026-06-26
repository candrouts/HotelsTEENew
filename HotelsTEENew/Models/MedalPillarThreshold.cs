using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_Medal_PillarThreshold")]
    public class MedalPillarThreshold
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public decimal medalID { get; set; }

        public decimal categoryID { get; set; }   // κύριος πυλώνας

        public decimal minValue { get; set; }

        public bool isPercent { get; set; }        // true = % του max του πυλώνα
    }
}
