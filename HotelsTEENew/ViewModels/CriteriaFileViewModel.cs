using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class CriteriaFileViewModel
    {


        public decimal id { get; set; }

        public decimal criteriaID { get; set; }

        public string title { get; set; }

        public bool isActive { get; set; }

        public string description { get; set; }

        public List<HotelCriteria_CriteriaFileViewModel> files { get; set; }
    }
}