using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    // Tokens μίας χρήσης για ενεργοποίηση λογαριασμού επιθεωρητή & επαναφορά κωδικού
    [Table("TEE_AccountTokens")]
    public class AccountToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public string email { get; set; }

        public decimal? inspectorID { get; set; }

        public string purpose { get; set; }         // "register" | "reset"

        public string tokenHash { get; set; }       // SHA256 hex — το token δεν αποθηκεύεται

        public DateTime expiresAt { get; set; }

        public DateTime? usedAt { get; set; }

        public string createdIP { get; set; }

        public DateTime createdDateTime { get; set; }
    }
}
