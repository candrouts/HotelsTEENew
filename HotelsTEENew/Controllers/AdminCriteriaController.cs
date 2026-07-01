using HotelsTEE.DAL;
using HotelsTEE.ViewModels;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class AdminCriteriaController : Controller
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        private bool IsAdmin()
        {
            string sql = "SELECT * FROM V_TEE_Users WHERE UserName = @UserName";
            UserViewModel user = unitOfWork.context.Database
                .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
            return user != null && user.role == 100;
        }

        public ActionResult Index()
        {
            if (!IsAdmin())
                return RedirectToAction("Index", "Home");

            ViewBag.CurrentView = "AdminCriteria/Index";
            return View();
        }
    }
}
