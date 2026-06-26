using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    [Table("TEE_Medals")]
    public class Medal
    {

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public decimal id { get; set; }

        public string title { get; set; }

        public int points { get; set; }


        public decimal min { get; set; }

        public decimal max { get; set; }

    }
}