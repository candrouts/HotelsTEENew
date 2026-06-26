using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Helpers;

namespace HotelsTEE.ViewModels
{
    public class HotelDetailsViewModel
    {

        
       
        public string hotelID { get; set; }

        public string hotelTitle { get; set; }
        public string hotelTitleEN { get; set; }
        public string hotelType { get; set; }
        public string category { get; set; }
        public string municipalityID { get; set; }
        public string periphereiaID { get; set; }   // kalID Περιφέρειας (ELSTATAreas levelID=3)
        public string peripheryID { get; set; }     // kalID Περιφερειακής Ενότητας (ELSTATAreas levelID=4)
        public int totalRooms { get; set; }
        public int totalBeds { get; set; }
        public string peripheryTitle { get; set; }
        public string periphereiaTitle { get; set; }
        public string municipalityTitle { get; set; }
        public string address { get; set; }
        public string toponymioFreeText { get; set; }

        public string zipCode { get; set; }
        public string emailForHCH { get; set; }
        public string phoneNumber1 { get; set; }
        public string type { get; set; }
        public string email { get; set; }
        public string mobilePhoneNumber { get; set; }
        public string legalRepresentativeName { get; set; }
        public string UserName { get; set; }


        public string exploitingCompanyID { get; set; }

        public string exploitingCompanyName { get; set; }

        public string taxNumber { get; set; }



    }
}