using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_Inspector_Areas")]
    public class InspectorArea
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public decimal inspectorID { get; set; }

        [ForeignKey("inspectorID")]
        public Inspector inspector { get; set; }

        public string kalID { get; set; }

        // 3 = Περιφέρεια, 4 = Περιφερειακή Ενότητα
        public int levelID { get; set; }
    }
}
