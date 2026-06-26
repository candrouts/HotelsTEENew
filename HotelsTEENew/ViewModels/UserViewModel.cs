using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class UserViewModel
    {

        public Guid UserId { get; set; }

        public string UserName { get; set; }

        public string AM { get; set; }

        public string exploitingCompanyID { get; set; }

        public int role { get; set; }

        public decimal? tee_inspectorID { get; set; }

    }
}