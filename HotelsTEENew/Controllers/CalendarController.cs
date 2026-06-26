using HotelsTEE.DAL;
using HotelsTEE.ViewModels;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class CalendarController : Controller
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        public ActionResult Index()
        {
            string sql = "SELECT * FROM V_TEE_Users WHERE UserName = @UserName";
            UserViewModel user = unitOfWork.context.Database
                .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();

            if (user == null)
                return RedirectToAction("Index", "Home");

            ViewBag.UserRole = user.role;
            ViewBag.CurrentView = "Calendar/Index";
            return View();
        }
    }
}
