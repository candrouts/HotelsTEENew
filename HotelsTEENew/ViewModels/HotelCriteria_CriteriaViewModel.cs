using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class HotelCriteria_CriteriaViewModel
    {

        public decimal id { get; set; }

        public decimal hotelCriteriaID { get; set; }

        public decimal criteriaID { get; set; }

        public bool? isChecked { get; set; }

        public string value { get; set; }

        public decimal? points { get; set; }

        public bool isApplicable { get; set; }

        public bool? isNotChecked { get; set; }

    }
}