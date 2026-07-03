using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Αποτέλεσμα AI προ-ελέγχου ενός μεταφορτωμένου τεκμηρίου.
    [Table("TEE_AI_DocumentCheck")]
    public class AiDocumentCheck
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal checkID { get; set; }

        public decimal hotelCriteriaFileID { get; set; }

        public string verdict { get; set; }      // ok | warn | fail (καταλληλότητα είδους)

        public string answerVerdict { get; set; }   // supported | unclear | contradicts | na (κάλυψη δηλωθείσας απάντησης)

        public string summary { get; set; }

        public string model { get; set; }

        public string checkedBy { get; set; }

        public DateTime checkedDateTime { get; set; }
    }
}
