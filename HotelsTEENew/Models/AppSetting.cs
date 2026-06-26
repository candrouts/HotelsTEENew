using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelsTEE.Models
{
    [Table("TEE_Settings")]
    public class AppSetting
    {
        [Key]
        public string settingKey { get; set; }

        public string settingValue { get; set; }
    }
}
