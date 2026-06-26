using HotelsTEE.DAL;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        public ActionResult Index()
        {
            ViewBag.CurrentView = "Home/Index";

            // Ρόλος server-side ώστε να μην "φλασάρει" το hero στους ξενοδόχους/admin
            int role = 0;
            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (user != null) role = user.role;
            }
            catch (Exception) { }

            ViewBag.UserRole = role;

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}