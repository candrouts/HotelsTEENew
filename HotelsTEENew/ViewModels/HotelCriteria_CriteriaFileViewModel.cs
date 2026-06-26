using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class HotelCriteria_CriteriaFileViewModel
    {

        public decimal id { get; set; }

        public decimal hotelCriteriaID { get; set; }

        public decimal criteriaFileID { get; set; }

        public string fileName { get; set; }

        public string fileType { get; set; }

        public DateTime creationDateTime { get; set; }
    }
}