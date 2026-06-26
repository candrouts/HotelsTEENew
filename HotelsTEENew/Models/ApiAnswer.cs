using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.Models
{
    public class ApiAnswer
    {

        public bool success { get; set; }

        public string responseText { get; set; }

        public decimal statusID { get; set; }

        public decimal id { get; set; }

        public DateTime? requestDateCompleted { get; set; }

        public bool isNotTheSame { get; set; }

        public string paymentStatus { get; set; }

        public bool thereIsNoUser { get; set; }

        public bool notFound { get; set; }

        public string reservationStatusTitle { get; set; }

        public DateTime? checkInDateTime { get; set; }

        public DateTime? checkOutDateTime { get; set; }

      
        public DateTime? cancelDateTime { get; set; }
    }
}