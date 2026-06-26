using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.Utils;
using HotelsTEE.ViewModels;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class CertificateDocController : Controller
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        private int Role()
        {
            string sql = "SELECT * FROM V_TEE_Users WHERE UserName = @UserName";
            UserViewModel user = unitOfWork.context.Database
                .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
            return user != null ? user.role : 0;
        }

        // Έκδοση (ή προβολή αν υπάρχει ήδη) — admin. Παράγει & αποθηκεύει στο CertificateFiles.
        public ActionResult Generate(decimal id = 0, bool force = false)
        {
            if (Role() != 100)
                return new HttpStatusCodeResult(403);

            HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(id);
            if (cert == null) return HttpNotFound();

            // Αν υπάρχει ήδη και δεν ζητήθηκε επανέκδοση → προβολή
            if (cert.certificateFileID.HasValue && !force)
                return RedirectToAction("View", new { id = id });

            CertificateDocResult doc = CertificateDocService.IssueAndStore(unitOfWork, id, force);
            if (!doc.success)
                return Content("Σφάλμα έκδοσης: " + doc.message);

            return RedirectToAction("View", new { id = id });
        }

        // Προβολή του αποθηκευμένου εγγράφου (admin ή ο ίδιος ο ξενοδόχος)
        [ActionName("View")]
        public ActionResult ViewDoc(decimal id = 0)
        {
            HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(id);
            if (cert == null || !cert.certificateFileID.HasValue) return HttpNotFound();

            // Έλεγχος πρόσβασης: admin ή ο ιδιοκτήτης του καταλύματος
            int role = Role();
            if (role != 100)
            {
                string sql = "Select top 1 hotelID, exploitingCompanyID from V_TEE_HotelDetails where UserName = @UserName";
                HotelDetailsViewModel own = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (own == null || own.hotelID != cert.hotelID || own.exploitingCompanyID != cert.exploitingCompanyID)
                    return new HttpStatusCodeResult(403);
            }

            CertificateFile file = unitOfWork.CertificateFileRepository.GetByID(cert.certificateFileID.Value);
            if (file == null || file.certificateFile == null) return HttpNotFound();

            string ext = (file.fileType ?? "html").ToLower();
            if (ext == "html" || ext == "htm")
            {
                // Εμφάνιση inline στον browser (έτοιμο για εκτύπωση/PDF)
                return Content(Encoding.UTF8.GetString(file.certificateFile), "text/html");
            }

            string mime = ext == "pdf" ? "application/pdf" : "application/octet-stream";
            return File(file.certificateFile, mime, (file.title ?? "certificate") + "." + ext);
        }
    }
}
