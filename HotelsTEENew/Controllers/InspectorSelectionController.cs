using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class InspectorSelectionController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.CurrentView = "InspectorSelection/Index";
            return View();
        }
    }
}
