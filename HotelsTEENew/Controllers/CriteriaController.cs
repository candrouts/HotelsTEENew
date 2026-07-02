using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    public class CriteriaController : Controller
    {
        // GET: Criteria
        public ActionResult Index()
        {
            ViewBag.CurrentView = "Criteria/Index";
            return View();
        }


        [Authorize]
        [HttpPost]
        public string PostFiles(List<HttpPostedFileBase> file, FormCollection collection)
        {

            string criteriaFileID = collection["criteriaFileID"];
            string hotelCriteriaID = collection["hotelCriteriaID"];



            if (criteriaFileID.Contains(','))
            {
                var c = criteriaFileID.Split(',');
                criteriaFileID = c[c.Length - 1];
            }


            if (hotelCriteriaID.Contains(','))
            {
                var c = hotelCriteriaID.Split(',');
                hotelCriteriaID = c[c.Length - 1];
            }


            HttpPostedFileBase fileToUpload = file.First();
            string folderToUpload = "";

            folderToUpload = criteriaFileID;



            try
            {
                AzureStorage.AzureStorage.SaveFileToPaintings(file, hotelCriteriaID, folderToUpload).Wait();

                
               

            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CriteriaController.cs");
                string ca = "";
            }

            return "ok";
        }
    }
}