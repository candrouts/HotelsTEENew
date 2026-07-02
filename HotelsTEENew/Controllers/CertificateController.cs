using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    public class CertificateController : Controller
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        // Λήψη τεκμηρίου από το Azure File Share
        [Authorize]
        public ActionResult GetFile(decimal id = 0)
        {
            try
            {
                HotelCriteria_CriteriaFile file = unitOfWork.HotelCriteria_CriteriaFileRepository.GetByID(id);
                if (file == null)
                    return HttpNotFound();

                var stream = AzureStorage.AzureStorage.GetFileFromFolder(
                    file.hotelCriteriaID.ToString(), file.criteriaFileID.ToString(), file.fileName);

                if (stream == null)
                    return HttpNotFound();

                string mime = MimeMapping.GetMimeMapping(file.fileName);
                return File(stream, mime, file.fileName);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "CertificateController.cs");
                return HttpNotFound();
            }
        }

        // GET: Certificate
        public ActionResult Index()
        {

            ViewBag.CurrentView = "Certificate/Index";
            return View();
        }

        // mode: 1 = προβολή αυτοαξιολόγησης (v1, read-only),
        //       2 = αυτοψία (v2), 3 = τελική κατάταξη (v3)
        // Ο ξενοδόχος (role=1) βλέπει πάντα read-only (η ιδιοκτησία ελέγχεται στο API).
        public ActionResult ViewCertificate(decimal id = 0, int mode = 2)
        {
            if (mode < 1 || mode > 3) mode = 2;

            bool readOnly = mode == 1;
            try
            {
                string sql = "SELECT * FROM [V_TEE_Users] WHERE UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new System.Data.SqlClient.SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (user != null && user.role == 1)
                    readOnly = true;
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "CertificateController.cs"); }

            ViewBag.CurrentView = "Certificate/ViewCertificate";
            ViewBag.Mode = mode;
            ViewBag.ReadOnly = readOnly;
            return View(id);
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
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateController.cs");
                string ca = "";
            }

            return "ok";
        }

    }
}