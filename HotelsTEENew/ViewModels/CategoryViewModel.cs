using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class CategoryViewModel
    {

        public decimal id { get; set; }

        public string title { get; set; }
      
        public string description { get; set; }
        
        public string examples { get; set; }

        public decimal? totalUnits { get; set; }

        public decimal? maxGrade { get; set; }

        public int order { get; set; }

        public bool isActive { get; set; }

        public decimal? parentID { get; set; }

        public List<CategoryViewModel> categories { get; set; }

        public List<CriteriaViewModel> criteria { get; set; }

    }
}