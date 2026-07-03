using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    public class AiDocCheckRequest
    {
        public decimal fileID { get; set; }            // TEE_HotelCriteria_CriteriaFiles.id
        public decimal hotelCriteriaID { get; set; }   // για GetDocumentChecks
    }

    // AI endpoints (ai branch / greencertai) — όλα πίσω από ai.enabled.
    [Authorize]
    public class AiApiController : ApiController
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        private UserViewModel CurrentUser()
        {
            string sql = "SELECT * FROM V_TEE_Users WHERE UserName = @UserName";
            return unitOfWork.context.Database
                .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
        }

        private bool IsAdmin()
        {
            UserViewModel user = CurrentUser();
            return user != null && user.role == 100;
        }

        // Επιτρέπεται: admin, ή ο ανατεθειμένος επιθεωρητής της αίτησης του τεκμηρίου.
        private bool CanCheck(HotelCriteria hc)
        {
            UserViewModel user = CurrentUser();
            if (user == null) return false;
            if (user.role == 100) return true;
            if (user.role != 10 || !user.tee_inspectorID.HasValue) return false;
            if (hc == null || !hc.certificateID.HasValue) return false;

            HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(hc.certificateID.Value);
            return cert != null && cert.tee_inspectorID == user.tee_inspectorID.Value;
        }

        // Διαγνωστικό: επαλήθευση σύνδεσης με Azure OpenAI (admin only).
        [Route("api/AiApi/Ping")]
        [HttpPost]
        public IHttpActionResult Ping()
        {
            try
            {
                if (!IsAdmin())
                    return Ok(new { success = false, message = "Δεν επιτρέπεται." });

                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο (ai.enabled=0 ή λείπουν endpoints/keys)." });

                string reply = Utils.AiService.Chat(
                    "Είσαι ο βοηθός του Συστήματος Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων.",
                    "Απάντησε ακριβώς με τη φράση: Η σύνδεση με το Azure OpenAI λειτουργεί.",
                    0m, 50);

                if (string.IsNullOrEmpty(reply))
                    return Ok(new { success = false, message = "Καμία απάντηση — δείτε το TEE_ErrorLog για το σφάλμα HTTP." });

                return Ok(new { success = true, message = reply });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.Ping");
                return Ok(new { success = false, message = "Σφάλμα — δείτε το TEE_ErrorLog." });
            }
        }

        // ── AI Προ-έλεγχος τεκμηρίου ─────────────────────────────────────
        // Κατεβάζει το αρχείο από το Azure Files, εξάγει κείμενο (Document
        // Intelligence) και το αξιολογεί με GPT σε σχέση με το κριτήριο και
        // το είδος τεκμηρίου. Αποθηκεύει verdict: ok | warn | fail.
        [Route("api/AiApi/CheckDocument")]
        [HttpPost]
        public IHttpActionResult CheckDocument([FromBody] AiDocCheckRequest req)
        {
            try
            {
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο." });
                if (req == null || req.fileID <= 0)
                    return Ok(new { success = false, message = "Λείπει το αρχείο." });

                HotelCriteria_CriteriaFile file = unitOfWork.HotelCriteria_CriteriaFileRepository.GetByID(req.fileID);
                if (file == null)
                    return Ok(new { success = false, message = "Δεν βρέθηκε το τεκμήριο." });

                HotelCriteria hc = unitOfWork.HotelCriteriaRepository.GetByID(file.hotelCriteriaID);
                if (!CanCheck(hc))
                    return Ok(new { success = false, message = "Δεν επιτρέπεται." });

                // Πλαίσιο: απαιτούμενο τεκμήριο + κριτήριο + κατάλυμα
                Criteria_File cf = unitOfWork.Criteria_FileRepository.GetByID(file.criteriaFileID);
                Criteria crit = cf != null ? unitOfWork.CriteriaRepository.GetByID(cf.criteriaID) : null;

                // Η απάντηση του ξενοδόχου στο κριτήριο (για την ίδια έκδοση αξιολόγησης)
                string declaredAnswer = "(δεν έχει απαντηθεί)";
                if (crit != null)
                {
                    decimal critId = crit.id;
                    HotelCriteria_Criteria ans = unitOfWork.HotelCriteria_CriteriaRepository
                        .Get(x => x.hotelCriteriaID == file.hotelCriteriaID && x.criteriaID == critId)
                        .FirstOrDefault();
                    if (ans != null)
                    {
                        if (ans.isApplicable == false)
                            declaredAnswer = "Δεν ισχύει (μη εφαρμόσιμο)";
                        else if (crit.criteriaType == 2)
                            declaredAnswer = string.IsNullOrEmpty(ans.value) ? "(κενή τιμή)" : ans.value;
                        else
                            declaredAnswer = ans.isChecked == true ? "ΝΑΙ" : (ans.isNotChecked == true ? "ΟΧΙ" : "(δεν έχει απαντηθεί)");
                    }
                }

                // Επιλογές του κριτηρίου (βοηθούν το μοντέλο να ερμηνεύσει την τιμή)
                string options = "";
                if (crit != null && !string.IsNullOrEmpty(crit.selectList)) options = crit.selectList;
                else if (crit != null && !string.IsNullOrEmpty(crit.gradesOptions)) options = crit.gradesOptions;
                if (options.Length > 800) options = options.Substring(0, 800);

                // Μεθοδολογία του κριτηρίου (περιέχει μονάδες/κανόνες υπολογισμού)
                string methodology = crit != null ? (crit.description ?? "") : "";
                if (methodology.Length > 2500) methodology = methodology.Substring(0, 2500);

                string hotelName = "";
                try
                {
                    string sqlH = "Select top 1 hotelTitle from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                    hotelName = unitOfWork.context.Database.SqlQuery<string>(sqlH,
                        new SqlParameter("@hotelID", hc.hotelID ?? ""),
                        new SqlParameter("@companyID", hc.exploitingCompanyID ?? "")).FirstOrDefault() ?? "";
                }
                catch (Exception) { }

                // Λήψη αρχείου από Azure Files
                byte[] bytes;
                using (Stream s = AzureStorage.AzureStorage.GetFileFromFolder(
                    file.hotelCriteriaID.ToString(), file.criteriaFileID.ToString(), file.fileName))
                using (var ms = new MemoryStream())
                {
                    if (s == null)
                        return Ok(new { success = false, message = "Το αρχείο δεν βρέθηκε στον αποθηκευτικό χώρο." });
                    s.CopyTo(ms);
                    bytes = ms.ToArray();
                }

                string ext = (Path.GetExtension(file.fileName) ?? "").TrimStart('.').ToLower();
                string contentType =
                    ext == "pdf" ? "application/pdf" :
                    (ext == "jpg" || ext == "jpeg") ? "image/jpeg" :
                    ext == "png" ? "image/png" :
                    ext == "docx" ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" :
                    "application/octet-stream";

                // OCR / εξαγωγή κειμένου
                string text = Utils.AiService.ExtractText(bytes, contentType);
                if (string.IsNullOrWhiteSpace(text))
                    return Ok(new { success = false, message = "Δεν ήταν δυνατή η ανάγνωση του εγγράφου (OCR)." });
                if (text.Length > 12000) text = text.Substring(0, 12000);

                // Κρίση GPT — αυστηρά JSON, διπλή ετυμηγορία (είδος + κάλυψη απάντησης)
                string system =
                    "Είσαι βοηθός επιθεωρητή στο Σύστημα Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων. " +
                    "Ελέγχεις ένα μεταφορτωμένο τεκμήριο σε ΔΥΟ επίπεδα και απαντάς ΑΥΣΤΗΡΑ με JSON " +
                    "{\"typeVerdict\":\"ok|warn|fail\",\"answerVerdict\":\"supported|unclear|contradicts|na\",\"summary\":\"...\"} χωρίς άλλο κείμενο.\n" +
                    "1) typeVerdict — καταλληλότητα είδους: ok=ανταποκρίνεται στο ζητούμενο τεκμήριο, warn=σχετικό με επιφυλάξεις (ελλιπές, παλιό, δεν αναφέρει την επιχείρηση), fail=άσχετο/ακατάλληλο.\n" +
                    "2) answerVerdict — αν το τεκμήριο υποστηρίζει τη ΔΗΛΩΘΕΙΣΑ ΑΠΑΝΤΗΣΗ του ξενοδόχου στο κριτήριο: " +
                    "supported=τα στοιχεία του εγγράφου επιβεβαιώνουν τη δήλωση, " +
                    "unclear=δεν επαρκούν τα στοιχεία ή απαιτούνται παραδοχές, " +
                    "contradicts=τα στοιχεία ΑΝΤΙΚΡΟΥΟΥΝ τη δήλωση με βεβαιότητα, " +
                    "na=δεν έχει νόημα η σύγκριση (π.χ. δεν υπάρχει απάντηση).\n" +
                    "ΚΑΝΟΝΕΣ: Αν χρειάζεται μετατροπή μονάδων ή υπολογισμός (π.χ. L/κύκλο ÷ χωρητικότητα kg = L/kg), ΚΑΝΕ τον και δείξε τον ΑΝΑΛΥΤΙΚΑ στο summary (π.χ. «53 L/κύκλο ÷ 6 kg = 8,8 L/kg < 10 ✔»). " +
                    "Να είσαι συντηρητικός: contradicts ΜΟΝΟ όταν ο υπολογισμός είναι αδιαμφισβήτητος· αν χρειάζονται παραδοχές (πρόγραμμα, φορτίο, πλήθος μηχανημάτων), απάντησε unclear και πες τι λείπει. " +
                    "Το summary στα ελληνικά, έως 3 προτάσεις, πρώτα ο τυχόν υπολογισμός.";

                string user =
                    "Κατάλυμα: " + hotelName + "\n" +
                    "Κριτήριο: " + (crit != null ? crit.code + " — " + crit.title : "-") + "\n" +
                    "Μεθοδολογία κριτηρίου: " + methodology + "\n" +
                    (options.Length > 0 ? "Επιλογές απάντησης κριτηρίου: " + options + "\n" : "") +
                    "ΔΗΛΩΘΕΙΣΑ ΑΠΑΝΤΗΣΗ ξενοδόχου: " + declaredAnswer + "\n" +
                    "Ζητούμενο τεκμήριο: " + (cf != null ? cf.title : "-") + "\n" +
                    "Περιγραφή ζητούμενου: " + (cf != null ? cf.description : "-") + "\n\n" +
                    "Κείμενο μεταφορτωμένου εγγράφου (" + file.fileName + "):\n" + text;

                string reply = Utils.AiService.Chat(system, user, 0m, 600);
                if (string.IsNullOrEmpty(reply))
                    return Ok(new { success = false, message = "Το μοντέλο δεν απάντησε — δείτε το TEE_ErrorLog." });

                // Ανθεκτικό parsing (αφαίρεση τυχόν ```json fences)
                string clean = reply.Trim();
                if (clean.StartsWith("```"))
                {
                    int i1 = clean.IndexOf('{'); int i2 = clean.LastIndexOf('}');
                    if (i1 >= 0 && i2 > i1) clean = clean.Substring(i1, i2 - i1 + 1);
                }
                string verdict = "warn", answerVerdict = "unclear", summary = reply;
                try
                {
                    JObject o = JObject.Parse(clean);
                    verdict = ((string)o["typeVerdict"] ?? (string)o["verdict"] ?? "warn").ToLower();
                    answerVerdict = ((string)o["answerVerdict"] ?? "unclear").ToLower();
                    summary = (string)o["summary"] ?? "";
                    if (verdict != "ok" && verdict != "warn" && verdict != "fail") verdict = "warn";
                    if (answerVerdict != "supported" && answerVerdict != "unclear"
                        && answerVerdict != "contradicts" && answerVerdict != "na") answerVerdict = "unclear";
                }
                catch (Exception) { }
                if (summary != null && summary.Length > 1500) summary = summary.Substring(0, 1500);

                var check = new AiDocumentCheck
                {
                    hotelCriteriaFileID = file.id,
                    verdict = verdict,
                    answerVerdict = answerVerdict,
                    summary = summary,
                    model = System.Configuration.ConfigurationManager.AppSettings["ai.openai.deployment"],
                    checkedBy = User.Identity.Name,
                    checkedDateTime = DateTime.Now
                };
                unitOfWork.AiDocumentCheckRepository.Insert(check);
                unitOfWork.Save();

                return Ok(new { success = true, fileID = file.id, verdict = verdict, answerVerdict = answerVerdict, summary = summary });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.CheckDocument");
                return Ok(new { success = false, message = "Σφάλμα ελέγχου — δείτε το TEE_ErrorLog." });
            }
        }

        // Τελευταίο αποτέλεσμα AI ελέγχου ανά τεκμήριο μιας έκδοσης αξιολόγησης.
        [Route("api/AiApi/GetDocumentChecks")]
        [HttpPost]
        public IHttpActionResult GetDocumentChecks([FromBody] AiDocCheckRequest req)
        {
            try
            {
                if (req == null || req.hotelCriteriaID <= 0)
                    return Ok(new { success = false });

                decimal hcId = req.hotelCriteriaID;
                List<decimal> fileIds = unitOfWork.HotelCriteria_CriteriaFileRepository
                    .Get(x => x.hotelCriteriaID == hcId).Select(x => x.id).ToList();

                var checks = unitOfWork.AiDocumentCheckRepository
                    .Get(x => fileIds.Contains(x.hotelCriteriaFileID))
                    .GroupBy(x => x.hotelCriteriaFileID)
                    .Select(g => g.OrderByDescending(x => x.checkedDateTime).First())
                    .Select(x => new { fileID = x.hotelCriteriaFileID, verdict = x.verdict, answerVerdict = x.answerVerdict, summary = x.summary })
                    .ToList();

                return Ok(new { success = true, checks = checks, aiEnabled = Utils.AiService.IsEnabled() });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.GetDocumentChecks");
                return Ok(new { success = false });
            }
        }
    }
}
