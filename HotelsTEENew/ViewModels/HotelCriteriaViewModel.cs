using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class HotelCriteriaViewModel
    {
        public DateTime? creationDatetime { get; set; }

        public DateTime? lastModificationDateTime { get; set; }

        public bool isFinished { get; set; }

        public decimal? totalScore { get; set; }

        public decimal id { get; set; }

        public string hotelID { get; set; }

        public string exploitingCompanyID { get; set; }

        public int status { get; set; }

        public int version { get; set; }

        public decimal totalPoints { get; set; }

        public decimal? medalID { get; set; }

        public decimal maxPoints { get; set; }

        public string medalTitle { get; set; }

        public decimal? certificateID { get; set; }

        public List<HotelCriteria_CriteriaViewModel> criteria { get; set; }

    }
}