using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class AdminInspectorsController : Controller
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

            ViewBag.CurrentView = "AdminInspectors/Index";
            return View();
        }

        // Μαζική εισαγωγή επιθεωρητών από CSV
        // Στήλες: firstName;lastName;email;phone;taxNumber (διαχωριστικό ; ή ,)
        [HttpPost]
        public ActionResult ImportCsv(HttpPostedFileBase csvFile)
        {
            if (!IsAdmin())
                return new HttpStatusCodeResult(403);

            int imported = 0, skipped = 0;
            var errors = new List<string>();

            try
            {
                if (csvFile == null || csvFile.ContentLength == 0)
                    return Json(new { success = false, message = "Δεν επιλέχθηκε αρχείο." });

                List<Inspector> existing = unitOfWork.InspectorRepository.Get().ToList();

                using (var reader = new StreamReader(csvFile.InputStream, Encoding.UTF8, true))
                {
                    string line;
                    int lineNo = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNo++;
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        char sep = line.Contains(';') ? ';' : ',';
                        var cols = line.Split(sep);

                        // Παράλειψη γραμμής επικεφαλίδων
                        if (lineNo == 1 && cols.Length > 0 &&
                            (cols[0].Trim().ToLower().Contains("firstname") || cols[0].Trim().Contains("Όνομα")))
                            continue;

                        if (cols.Length < 3)
                        {
                            errors.Add("Γραμμή " + lineNo + ": λιγότερες από 3 στήλες");
                            skipped++;
                            continue;
                        }

                        string firstName = cols[0].Trim();
                        string lastName = cols[1].Trim();
                        string email = cols[2].Trim();
                        string phone = cols.Length > 3 ? cols[3].Trim() : "";
                        string taxNumber = cols.Length > 4 ? cols[4].Trim() : "";

                        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(email))
                        {
                            errors.Add("Γραμμή " + lineNo + ": κενό όνομα/επώνυμο/email");
                            skipped++;
                            continue;
                        }

                        // Αποφυγή διπλοεγγραφών με βάση το email
                        if (existing.Any(x => x.email != null && x.email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                        {
                            skipped++;
                            continue;
                        }

                        Inspector inspector = new Inspector
                        {
                            firstName = firstName,
                            lastName = lastName,
                            email = email,
                            phone = phone,
                            taxNumber = taxNumber,
                            isActive = true
                        };
                        unitOfWork.InspectorRepository.Insert(inspector);
                        existing.Add(inspector);
                        imported++;
                    }
                }

                unitOfWork.Save();

                return Json(new
                {
                    success = true,
                    imported = imported,
                    skipped = skipped,
                    errors = errors
                });
            }
            catch (Exception e)
            {
                return Json(new { success = false, message = e.Message });
            }
        }
    }
}
