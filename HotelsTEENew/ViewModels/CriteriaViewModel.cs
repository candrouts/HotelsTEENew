using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class CriteriaViewModel
    {

        public decimal id { get; set; }

        public string title { get; set; }

        public string description { get; set; }

        public decimal categoryID { get; set; }

        public int order { get; set; }

        public string code { get; set; }

        public int weight { get; set; }

        public decimal maxGrade { get; set; }

        public int criteriaType { get; set; }

        public string gradesList { get; set; }

        public string selectList { get; set; } 

        public string notes1 { get; set; }

        public string notes2 { get; set; }

        public string categoryTitle { get; set; }

        public DateTime dateFrom { get; set; }

        public DateTime dateTo { get; set; }

        public string gradesOptions { get; set; }

        public bool? needsFiles { get; set; }

        

        public bool? notApplicable { get; set; }

        public List<CriteriaFileViewModel> files { get; set; }



        public bool? isChecked { get; set; }

        public string value { get; set; }

        public decimal? points { get; set; }

        public bool isApplicable { get; set; }

        public bool? isNotChecked { get; set; }

        public bool? isRequired { get; set; }

    }
}