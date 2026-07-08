using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Cache embedding ενός κριτηρίου για τη σημασιολογική αναζήτηση.
    [Table("TEE_AI_CriteriaEmbedding")]
    public class AiCriteriaEmbedding
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public decimal criteriaID { get; set; }

        public string contentHash { get; set; }

        public string embedding { get; set; }   // JSON array float

        public DateTime updatedDateTime { get; set; }
    }
}
