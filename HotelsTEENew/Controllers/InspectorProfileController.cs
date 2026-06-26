using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class InspectorProfileController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.CurrentView = "InspectorProfile/Index";
            return View();
        }
    }
}
