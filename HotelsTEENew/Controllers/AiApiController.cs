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

    public class AiChatMessage
    {
        public string role { get; set; }      // user | assistant
        public string content { get; set; }
    }

    public class AiAdviseRequest
    {
        public System.Collections.Generic.List<AiChatMessage> messages { get; set; }
    }

    public class AiReportRequest
    {
        public decimal certificateID { get; set; }
    }

    public class AiSearchRequest
    {
        public string query { get; set; }
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

                var outcome = Utils.AiDocumentChecker.Run(unitOfWork, file, hc, User.Identity.Name);
                if (!outcome.success)
                    return Ok(new { success = false, message = outcome.message });

                return Ok(new { success = true, fileID = file.id, verdict = outcome.verdict, answerVerdict = outcome.answerVerdict, summary = outcome.summary });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.CheckDocument");
                return Ok(new { success = false, message = "Σφάλμα ελέγχου — δείτε το TEE_ErrorLog." });
            }
        }

        // Διαθεσιμότητα του AI Συμβούλου για τον τρέχοντα χρήστη (global widget).
        [Route("api/AiApi/ChatAvailable")]
        [HttpPost]
        public IHttpActionResult ChatAvailable()
        {
            try
            {
                if (!Utils.AiService.IsEnabled()) return Ok(new { enabled = false });
                UserViewModel user = CurrentUser();
                return Ok(new { enabled = user != null && user.role == 1 });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.ChatAvailable");
                return Ok(new { enabled = false });
            }
        }

        // ── AI Σύμβουλος Βιωσιμότητας (ξενοδόχος) ───────────────────────
        // Multi-turn chat με πλήρες context: κριτήρια, απαντήσεις, βαθμολογία,
        // μετάλλια/βάσεις πυλώνων. Ημερήσιο όριο μηνυμάτων ανά χρήστη.
        [Route("api/AiApi/Advise")]
        [HttpPost]
        public IHttpActionResult Advise([FromBody] AiAdviseRequest req)
        {
            try
            {
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Ο AI Σύμβουλος δεν είναι διαθέσιμος." });

                UserViewModel user = CurrentUser();
                if (user == null || user.role != 1)
                    return Ok(new { success = false, message = "Ο Σύμβουλος είναι διαθέσιμος μόνο σε ξενοδόχους." });

                if (req == null || req.messages == null || req.messages.Count == 0)
                    return Ok(new { success = false, message = "Κενό μήνυμα." });

                // Ημερήσιο όριο (έλεγχος κόστους)
                DateTime today = DateTime.Today;
                string uname = User.Identity.Name;
                int todayCount = unitOfWork.AiChatLogRepository
                    .Get(x => x.userName == uname && x.logDateTime >= today).Count();
                if (todayCount >= 40)
                    return Ok(new { success = false, message = "Συμπληρώσατε το ημερήσιο όριο ερωτήσεων. Δοκιμάστε ξανά αύριο." });

                // ── Context: κατάλυμα + ενεργή αυτοαξιολόγηση ────────────
                string sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
                HotelDetailsViewModel hotel = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", uname))
                    .FirstOrDefault();
                if (hotel == null)
                    return Ok(new { success = false, message = "Δεν βρέθηκε το κατάλυμα." });

                HotelCriteria v1 = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.hotelID == hotel.hotelID && x.exploitingCompanyID == hotel.exploitingCompanyID
                           && x.version == 1 && x.isFinished == false)
                    .FirstOrDefault();

                List<HotelCriteria_Criteria> answers = v1 != null
                    ? unitOfWork.HotelCriteria_CriteriaRepository.Get(x => x.hotelCriteriaID == v1.id).ToList()
                    : new List<HotelCriteria_Criteria>();
                Dictionary<decimal, HotelCriteria_Criteria> ansByCrit = answers
                    .GroupBy(a => a.criteriaID).ToDictionary(g => g.Key, g => g.First());

                // Δομή: πυλώνες/υποπυλώνες + ενεργά κριτήρια
                List<CategoryViewModel> cats = unitOfWork.context.Database
                    .SqlQuery<CategoryViewModel>("Select * from V_TEE_Categories where isActive=1").ToList();
                Dictionary<decimal, CategoryViewModel> catById = cats.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());

                List<Criteria> crits = unitOfWork.CriteriaRepository
                    .Get(c => c.dateFrom <= DateTime.Now && c.dateTo >= DateTime.Now).ToList();

                // Τρέχουσα βαθμολογία/μετάλλιο (αν υπάρχει ενεργή αυτοαξιολόγηση) — χωρίς Save
                string scoreInfo = "Δεν υπάρχει ενεργή αυτοαξιολόγηση σε εξέλιξη.";
                if (v1 != null && answers.Count > 0)
                {
                    var eval = Utils.ScoringHelper.ApplyMedal(unitOfWork, v1, answers);
                    Medal awarded = v1.medalID.HasValue ? unitOfWork.MedalRepository.GetByID(v1.medalID.Value) : null;
                    scoreInfo = "Τρέχουσα συνολική βαθμολογία: " + (v1.totalScore ?? 0).ToString("0.##") + "/95. " +
                                "Τρέχον μετάλλιο: " + (awarded != null ? awarded.title : "-") + ".";
                }

                // Μετάλλια + όρια
                List<Medal> medals = unitOfWork.MedalRepository.Get().OrderBy(m => m.min).ToList();
                string medalsInfo = string.Join(" | ", medals.Select(m => m.title + ": " + m.min + "-" + m.max));

                // ── Κατάσταση τρέχουσας αίτησης (στάδιο ροής) ────────────
                string appStatus;
                if (v1 == null)
                    appStatus = "Δεν υπάρχει ενεργή διαδικασία αξιολόγησης.";
                else if (v1.status == 1)
                    appStatus = "Η αυτοαξιολόγηση είναι ΣΕ ΕΞΕΛΙΞΗ — δεν έχει υποβληθεί οριστικά ακόμη.";
                else if (!v1.certificateID.HasValue)
                    appStatus = "Η αυτοαξιολόγηση έχει υποβληθεί. ΕΚΚΡΕΜΕΙ η υποβολή αίτησης και η επιλογή επιθεωρητή (σελίδα «Υποβολή Αίτησης»).";
                else
                {
                    HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(v1.certificateID.Value);
                    decimal certID = v1.certificateID.Value;
                    HotelCriteria v2s = unitOfWork.HotelCriteriaRepository
                        .Get(x => x.certificateID == certID && x.version == 2).FirstOrDefault();
                    HotelCriteria v3s = unitOfWork.HotelCriteriaRepository
                        .Get(x => x.certificateID == certID && x.version == 3).FirstOrDefault();

                    string inspName = "-";
                    if (cert != null && cert.tee_inspectorID.HasValue)
                    {
                        Inspector insp = unitOfWork.InspectorRepository.GetByID(cert.tee_inspectorID.Value);
                        if (insp != null) inspName = insp.firstName + " " + insp.lastName;
                    }
                    string autopsyInfo = cert != null && cert.autopsyDateTime.HasValue
                        ? cert.autopsyDateTime.Value.ToString("dd/MM/yyyy") +
                          (cert.autopsyDateStatus == 1 ? " (προτεινόμενη — εκκρεμεί επιβεβαίωση από τον επιθεωρητή)" : " (οριστική)")
                        : "δεν έχει οριστεί";

                    if (cert == null)
                        appStatus = "Η αίτηση δεν βρέθηκε.";
                    else if (cert.certificateStatusID == 2)
                        appStatus = "Η αξιολόγηση ΟΛΟΚΛΗΡΩΘΗΚΕ και η βεβαίωση έχει εκδοθεί" +
                            (cert.issueDateTime.HasValue ? " στις " + cert.issueDateTime.Value.ToString("dd/MM/yyyy") : "") +
                            (cert.validityStopDateTime.HasValue ? " με ισχύ έως " + cert.validityStopDateTime.Value.ToString("dd/MM/yyyy") : "") +
                            ". Είναι διαθέσιμη στην Αρχική Σελίδα («Οι Βεβαιώσεις μου»).";
                    else if (cert.certificateStatusID == 24)
                        appStatus = "Ο επιθεωρητής ΑΠΕΡΡΙΨΕ την ανάθεση — απαιτείται επιλογή νέου επιθεωρητή από τη σελίδα «Υποβολή Αίτησης».";
                    else if (v3s != null && v3s.status == 2)
                        appStatus = "Ο επιθεωρητής (" + inspName + ") ολοκλήρωσε την ΤΕΛΙΚΗ ΚΑΤΑΤΑΞΗ. ΕΚΚΡΕΜΕΙ Η ΔΙΚΗ ΣΑΣ ΑΠΟΔΟΧΗ στη σελίδα «Υποβολή Αίτησης» — με την αποδοχή σας η βεβαίωση εκδίδεται ΑΜΕΣΑ και αυτόματα.";
                    else if (v3s != null && v3s.status == 1)
                        appStatus = "Η αυτοψία ολοκληρώθηκε και ο επιθεωρητής (" + inspName + ") συντάσσει την τελική κατάταξη.";
                    else if (v2s != null && v2s.status == 2)
                        appStatus = "Η αυτοψία ολοκληρώθηκε — αναμένεται η τελική κατάταξη από τον επιθεωρητή (" + inspName + ").";
                    else if (v2s != null)
                        appStatus = "Η ΑΥΤΟΨΙΑ είναι σε εξέλιξη από τον επιθεωρητή " + inspName + ". Ημερομηνία αυτοψίας: " + autopsyInfo + ".";
                    else
                        appStatus = "Η αίτηση ανατέθηκε στον επιθεωρητή " + inspName + ". Ημερομηνία αυτοψίας: " + autopsyInfo + ".";
                }

                // Ενεργοί επιθεωρητές (μόνο όνομα + περιοχές ευθύνης — ΚΑΝΕΝΑ στοιχείο
                // σύγκρισης) σε ΤΥΧΑΙΑ σειρά ανά κλήση, ώστε καμία θέση στη λίστα να μη
                // δίνει συστηματικό πλεονέκτημα (αμεροληψία by design).
                string inspectorsInfo = "";
                try
                {
                    string sqlIns = @"
                        SELECT i.id, i.firstName, i.lastName, i.email, i.phone,
                            STUFF((
                                SELECT ', ' + ea2.title
                                FROM TEE_Inspector_Areas ia2
                                INNER JOIN ELSTATAreas ea2 ON ea2.kalID = ia2.kalID
                                WHERE ia2.inspectorID = i.id
                                ORDER BY ea2.levelID, ea2.title
                                FOR XML PATH(''), TYPE
                            ).value('.','NVARCHAR(MAX)'), 1, 2, '') AS areas
                        FROM TEE_Inspectors i
                        WHERE i.isActive = 1";
                    var inspectors = unitOfWork.context.Database
                        .SqlQuery<InspectorSearchViewModel>(sqlIns).ToList();

                    var rnd = new Random();
                    var shuffled = inspectors.OrderBy(x => rnd.Next()).ToList();

                    inspectorsInfo = "ΕΝΕΡΓΟΙ ΕΠΙΘΕΩΡΗΤΕΣ ΜΗΤΡΩΟΥ (σύνολο: " + shuffled.Count + ") — όνομα → περιοχές ευθύνης:\n" +
                        string.Join("\n", shuffled.Select(i =>
                            "- " + i.firstName + " " + i.lastName + " → " + (string.IsNullOrEmpty(i.areas) ? "(χωρίς δηλωμένες περιοχές)" : i.areas)));
                }
                catch (Exception exIns) { HotelsTEE.Utils.ErrorLogger.Log(exIns, "AiApiController.Advise.Inspectors"); }

                // Γραμμές κριτηρίων (συμπυκνωμένες): code|τίτλος|πυλώνας|max μονάδες|απάντηση
                var lines = new List<string>();
                foreach (var c in crits.OrderBy(c => c.categoryID).ThenBy(c => c.order))
                {
                    CategoryViewModel sub; catById.TryGetValue(c.categoryID, out sub);
                    CategoryViewModel pillar = null;
                    if (sub != null && sub.parentID.HasValue) catById.TryGetValue(sub.parentID.Value, out pillar);

                    string answer = "(αναπάντητο)";
                    HotelCriteria_Criteria a;
                    if (ansByCrit.TryGetValue(c.id, out a))
                    {
                        if (a.isApplicable == false) answer = "Δεν ισχύει";
                        else if (c.criteriaType == 2) answer = string.IsNullOrEmpty(a.value) ? "(αναπάντητο)" : ("τιμή=" + a.value + " (" + (a.points ?? 0) + "μ)");
                        else answer = a.isChecked == true ? ("ΝΑΙ (" + (a.points ?? 0) + "μ)") : (a.isNotChecked == true ? "ΟΧΙ (0μ)" : "(αναπάντητο)");
                    }

                    lines.Add(c.code + "|" + c.title + "|" + (pillar != null ? pillar.title : "-") +
                              "|max " + (c.weight * c.maxGrade) + "μ" +
                              (c.isRequired == true ? "|ΥΠΟΧΡΕΩΤΙΚΟ" : "") + "|" + answer);
                }

                string system =
                    "Είσαι ο «AI Σύμβουλος Βιωσιμότητας» του Συστήματος Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων (ΞΕΕ). " +
                    "Βοηθάς τον ξενοδόχο να καταλάβει τα κριτήρια, να βελτιώσει τη βαθμολογία του και να ετοιμάσει τα σωστά τεκμήρια.\n" +
                    "ΚΑΝΟΝΕΣ:\n" +
                    "- Απαντάς ΜΟΝΟ για το σύστημα πιστοποίησης, τα κριτήρια, τη βαθμολογία και τα τεκμήρια. Για οτιδήποτε άλλο, αρνήσου ευγενικά.\n" +
                    "- Βασίζεσαι ΑΠΟΚΛΕΙΣΤΙΚΑ στα δεδομένα που σου δίνονται — μην επινοείς κριτήρια ή βαθμούς.\n" +
                    "- Όταν προτείνεις βελτιώσεις, ξεκίνα από αναπάντητα/αρνητικά κριτήρια με τους περισσότερους βαθμούς και ανάφερε τους κωδικούς τους.\n" +
                    "- Οι βαθμοί κριτηρίων είναι αρχικοί (raw)· η συνολική βαθμολογία 0-95 προκύπτει με αναγωγή ανά πυλώνα — μίλα ενδεικτικά, μην υπόσχεσαι ακριβή τελικά νούμερα.\n" +
                    "ΚΑΝΟΝΕΣ ΓΙΑ ΕΠΙΘΕΩΡΗΤΕΣ (ΑΥΣΤΗΡΗ ΟΥΔΕΤΕΡΟΤΗΤΑ):\n" +
                    "- Όταν ρωτηθείς για επιθεωρητές μιας περιοχής, ανάφερε ΟΛΟΥΣ όσους την καλύπτουν (και το πλήθος τους), ΠΑΝΤΑ σε αλφαβητική σειρά επωνύμου (ουδέτερη σειρά μητρώου), χωρίς καμία σύσταση, σύγκριση ή αξιολόγηση.\n" +
                    "- Όταν ο ξενοδόχος λέει «η περιοχή μου», εννοεί την περιοχή του καταλύματός του (δίνεται στα στοιχεία του) — μην ξαναρωτάς την περιοχή.\n" +
                    "- Αν ρωτηθείς «ποιον να διαλέξω» ή «ποιος είναι καλύτερος/γρηγορότερος», απάντησε ότι όλοι οι πιστοποιημένοι επιθεωρητές του μητρώου είναι ισότιμοι και η επιλογή είναι αποκλειστικά δική του — δεν διαθέτεις και δεν παρέχεις συγκριτικά στοιχεία.\n" +
                    "- Μην δίνεις στοιχεία επικοινωνίας — για στοιχεία, αναζήτηση και υποβολή αίτησης παραπέμπεις πάντα στη σελίδα «Υποβολή Αίτησης».\n" +
                    "- Απαντάς στα ελληνικά, φιλικά και συνοπτικά (έως ~150 λέξεις), με λίστες όπου βοηθά.\n\n" +
                    "ΣΤΟΙΧΕΙΑ ΚΑΤΑΛΥΜΑΤΟΣ: " + hotel.hotelTitle + ", κατηγορία " + hotel.category +
                    ", " + hotel.totalRooms + " δωμάτια / " + hotel.totalBeds + " κλίνες. " +
                    "Περιοχή καταλύματος: " + (hotel.periphereiaTitle ?? "-") +
                    ", Π.Ε. " + (hotel.peripheryTitle ?? "-") +
                    ", Δήμος " + (hotel.municipalityTitle ?? "-") + ".\n" +
                    scoreInfo + "\nΚλίμακα μεταλλίων (συνολική 0-95): " + medalsInfo + "\n" +
                    "ΚΑΤΑΣΤΑΣΗ ΤΡΕΧΟΥΣΑΣ ΑΙΤΗΣΗΣ: " + appStatus + "\n" +
                    "ΡΟΗ ΔΙΑΔΙΚΑΣΙΑΣ (για ερωτήσεις «σε ποια φάση είμαι / πότε παίρνω βεβαίωση»): " +
                    "1) Αυτοαξιολόγηση → 2) Υποβολή αίτησης & επιλογή επιθεωρητή → 3) Αυτοψία από επιθεωρητή → " +
                    "4) Τελική κατάταξη από επιθεωρητή → 5) Αποδοχή τελικής κατάταξης από τον ξενοδόχο → " +
                    "6) ΑΥΤΟΜΑΤΗ έκδοση βεβαίωσης με την αποδοχή (ισχύς 3 έτη). " +
                    "Δεν υπάρχουν εγγυημένα χρονοδιαγράμματα για τα βήματα του επιθεωρητή — μην υπόσχεσαι ημερομηνίες που δεν δίνονται.\n\n" +
                    (inspectorsInfo.Length > 0 ? inspectorsInfo + "\n\n" : "") +
                    "ΚΡΙΤΗΡΙΑ (κωδικός|τίτλος|πυλώνας|μέγιστοι βαθμοί|απάντηση):\n" + string.Join("\n", lines);

                // Ιστορικό: τα τελευταία 12 μηνύματα, με όρια μήκους
                var history = new List<KeyValuePair<string, string>>();
                foreach (var m in req.messages.Skip(Math.Max(0, req.messages.Count - 12)))
                {
                    string role = m.role == "assistant" ? "assistant" : "user";
                    string content = (m.content ?? "");
                    if (content.Length > 1500) content = content.Substring(0, 1500);
                    history.Add(new KeyValuePair<string, string>(role, content));
                }

                string reply = Utils.AiService.ChatConversation(system, history, 0.3m, 900);
                if (string.IsNullOrEmpty(reply))
                    return Ok(new { success = false, message = "Ο Σύμβουλος δεν απάντησε — δοκιμάστε ξανά." });

                // Καταγραφή (τελευταία ερώτηση + απάντηση)
                try
                {
                    string lastQ = history.Count > 0 ? history[history.Count - 1].Value : "";
                    if (lastQ.Length > 2000) lastQ = lastQ.Substring(0, 2000);
                    unitOfWork.AiChatLogRepository.Insert(new AiChatLog
                    {
                        userName = uname,
                        question = lastQ,
                        answer = reply,
                        logDateTime = DateTime.Now
                    });
                    unitOfWork.Save();
                }
                catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AiApiController.Advise.Log"); }

                return Ok(new { success = true, reply = reply });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.Advise");
                return Ok(new { success = false, message = "Σφάλμα — δοκιμάστε ξανά." });
            }
        }

        // Κοινό: συμπυκνωμένες γραμμές απαντήσεων μιας έκδοσης, ομαδοποιημένες ανά πυλώνα.
        private string BuildAnswerLines(HotelCriteria hc, out string hotelName)
        {
            hotelName = "";
            try
            {
                string sqlH = "Select top 1 hotelTitle from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                hotelName = unitOfWork.context.Database.SqlQuery<string>(sqlH,
                    new SqlParameter("@hotelID", hc.hotelID ?? ""),
                    new SqlParameter("@companyID", hc.exploitingCompanyID ?? "")).FirstOrDefault() ?? "";
            }
            catch (Exception) { }

            List<HotelCriteria_Criteria> answers = unitOfWork.HotelCriteria_CriteriaRepository
                .Get(x => x.hotelCriteriaID == hc.id).ToList();
            Dictionary<decimal, HotelCriteria_Criteria> ansByCrit = answers
                .GroupBy(a => a.criteriaID).ToDictionary(g => g.Key, g => g.First());

            List<CategoryViewModel> cats = unitOfWork.context.Database
                .SqlQuery<CategoryViewModel>("Select * from V_TEE_Categories where isActive=1").ToList();
            Dictionary<decimal, CategoryViewModel> catById = cats.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());

            List<Criteria> crits = unitOfWork.CriteriaRepository
                .Get(c => c.dateFrom <= DateTime.Now && c.dateTo >= DateTime.Now).ToList();

            var lines = new List<string>();
            foreach (var c in crits.OrderBy(c => c.categoryID).ThenBy(c => c.order))
            {
                CategoryViewModel sub; catById.TryGetValue(c.categoryID, out sub);
                CategoryViewModel pillar = null;
                if (sub != null && sub.parentID.HasValue) catById.TryGetValue(sub.parentID.Value, out pillar);

                string answer = "(αναπάντητο)";
                HotelCriteria_Criteria a;
                if (ansByCrit.TryGetValue(c.id, out a))
                {
                    if (a.isApplicable == false) answer = "Δεν ισχύει";
                    else if (c.criteriaType == 2) answer = string.IsNullOrEmpty(a.value) ? "(αναπάντητο)" : ("τιμή=" + a.value + " (" + (a.points ?? 0) + "μ)");
                    else answer = a.isChecked == true ? ("ΝΑΙ (" + (a.points ?? 0) + "μ)") : (a.isNotChecked == true ? "ΟΧΙ" : "(αναπάντητο)");
                }

                lines.Add(c.code + "|" + c.title + "|" + (pillar != null ? pillar.title : "-") + "|" + answer);
            }
            return string.Join("\n", lines);
        }

        // ── #3α: Σχέδιο τεχνικής έκθεσης αυτοψίας (επιθεωρητής/admin) ──
        [Route("api/AiApi/DraftReport")]
        [HttpPost]
        public IHttpActionResult DraftReport([FromBody] AiReportRequest req)
        {
            try
            {
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο." });
                if (req == null || req.certificateID <= 0)
                    return Ok(new { success = false, message = "Λείπει η αίτηση." });

                // Έκδοση αυτοψίας (v2) — εκεί βασίζεται η έκθεση· fallback στην τελική (v3)
                HotelCriteria hc = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == req.certificateID && x.version == 2).FirstOrDefault()
                    ?? unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == req.certificateID && x.version == 3).FirstOrDefault();
                if (hc == null)
                    return Ok(new { success = false, message = "Δεν υπάρχει αυτοψία για αυτή την αίτηση." });

                if (!CanCheck(hc))
                    return Ok(new { success = false, message = "Δεν επιτρέπεται." });

                string hotelName;
                string lines = BuildAnswerLines(hc, out hotelName);

                string medalTitle = "-";
                if (hc.medalID.HasValue)
                {
                    Medal m = unitOfWork.MedalRepository.GetByID(hc.medalID.Value);
                    if (m != null) medalTitle = m.title;
                }

                HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(req.certificateID);
                string autopsyDate = cert != null && cert.autopsyDateTime.HasValue
                    ? cert.autopsyDateTime.Value.ToString("dd/MM/yyyy") : "-";

                // Σύνοψη AI ελέγχων τεκμηρίων (αν υπάρχουν)
                decimal hcId = hc.id;
                List<decimal> fileIds = unitOfWork.HotelCriteria_CriteriaFileRepository
                    .Get(x => x.hotelCriteriaID == hcId).Select(x => x.id).ToList();
                var docChecks = unitOfWork.AiDocumentCheckRepository
                    .Get(x => fileIds.Contains(x.hotelCriteriaFileID)).ToList();
                string docSummary = docChecks.Count == 0 ? "Δεν έχουν γίνει AI έλεγχοι τεκμηρίων." :
                    "AI έλεγχοι τεκμηρίων: " + docChecks.Count(x => x.verdict == "ok") + " κατάλληλα, " +
                    docChecks.Count(x => x.verdict == "warn") + " με επιφύλαξη, " +
                    docChecks.Count(x => x.verdict == "fail") + " ακατάλληλα.";

                string system =
                    "Είσαι βοηθός επιθεωρητή του Συστήματος Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων. " +
                    "Συντάσσεις ΣΧΕΔΙΟ τεχνικής έκθεσης αυτοψίας στα ελληνικά, σε επίσημο αλλά απλό ύφος, " +
                    "βασισμένο ΑΠΟΚΛΕΙΣΤΙΚΑ στα δεδομένα που δίνονται (μην επινοείς ευρήματα). Δομή:\n" +
                    "1. ΕΙΣΑΓΩΓΗ (κατάλυμα, ημερομηνία αυτοψίας, αντικείμενο)\n" +
                    "2. ΕΥΡΗΜΑΤΑ ΑΝΑ ΠΥΛΩΝΑ (σύνοψη δυνατών σημείων και ελλείψεων, με αναφορά κωδικών κριτηρίων)\n" +
                    "3. ΤΕΚΜΗΡΙΩΣΗ (κατάσταση τεκμηρίων)\n" +
                    "4. ΣΥΜΠΕΡΑΣΜΑ (συνολική εικόνα, βαθμολογία και προτεινόμενη βαθμίδα)\n" +
                    "Στο τέλος γράψε: «Το παρόν αποτελεί σχέδιο που συντάχθηκε με υποβοήθηση AI και τελεί υπό την έγκριση του επιθεωρητή.» " +
                    "Έκταση ~300-400 λέξεις.";

                string user =
                    "Κατάλυμα: " + hotelName + "\n" +
                    "Ημερομηνία αυτοψίας: " + autopsyDate + "\n" +
                    "Συνολική βαθμολογία (αναγμένη): " + (hc.totalScore ?? 0).ToString("0.##") + "/95, Βαθμίδα: " + medalTitle + "\n" +
                    docSummary + "\n\n" +
                    "ΑΠΑΝΤΗΣΕΙΣ ΚΡΙΤΗΡΙΩΝ (κωδικός|τίτλος|πυλώνας|απάντηση):\n" + lines;

                string reply = Utils.AiService.Chat(system, user, 0.3m, 1600);
                if (string.IsNullOrEmpty(reply))
                    return Ok(new { success = false, message = "Το μοντέλο δεν απάντησε — δοκιμάστε ξανά." });

                return Ok(new { success = true, text = reply });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.DraftReport");
                return Ok(new { success = false, message = "Σφάλμα — δείτε το TEE_ErrorLog." });
            }
        }

        // ── #3β: «Πράσινο Προφίλ» καταλύματος (ξενοδόχος) ───────────────
        // Marketing κείμενο (ελληνικά + αγγλικά) από τα επιτεύγματα της
        // πιο πρόσφατης ολοκληρωμένης αξιολόγησης.
        [Route("api/AiApi/GreenProfile")]
        [HttpPost]
        public IHttpActionResult GreenProfile([FromBody] AiReportRequest req)
        {
            try
            {
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο." });

                UserViewModel user = CurrentUser();
                if (user == null || user.role != 1)
                    return Ok(new { success = false, message = "Διαθέσιμο μόνο σε ξενοδόχους." });

                string sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
                HotelDetailsViewModel hotel = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (hotel == null)
                    return Ok(new { success = false, message = "Δεν βρέθηκε το κατάλυμα." });

                // Τελική κατάταξη (v3) της ζητούμενης αίτησης — με έλεγχο ιδιοκτησίας
                HotelCriteria v3 = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == req.certificateID && x.version == 3
                           && x.hotelID == hotel.hotelID && x.exploitingCompanyID == hotel.exploitingCompanyID)
                    .FirstOrDefault();
                if (v3 == null)
                    return Ok(new { success = false, message = "Δεν βρέθηκε ολοκληρωμένη αξιολόγηση." });

                string hotelName;
                string lines = BuildAnswerLines(v3, out hotelName);

                string medalTitle = "-";
                if (v3.medalID.HasValue)
                {
                    Medal m = unitOfWork.MedalRepository.GetByID(v3.medalID.Value);
                    if (m != null) medalTitle = m.title;
                }

                string system =
                    "Είσαι copywriter τουρισμού. Γράφεις το «Πράσινο Προφίλ» ενός καταλύματος: " +
                    "ένα ελκυστικό κείμενο για το site/booking του, βασισμένο ΑΠΟΚΛΕΙΣΤΙΚΑ στα πιστοποιημένα επιτεύγματά του " +
                    "(κριτήρια με ΝΑΙ ή θετική τιμή) — ΠΟΤΕ σε ελλείψεις ή αναπάντητα, και χωρίς υπερβολές/greenwashing.\n" +
                    "Δομή απάντησης:\n" +
                    "=== ΕΛΛΗΝΙΚΑ ===\n(τίτλος + 2-3 παράγραφοι + 4-6 bullets με τα δυνατά σημεία)\n" +
                    "=== ENGLISH ===\n(ίδιο περιεχόμενο στα αγγλικά)\n" +
                    "Ανάφερε τη βαθμίδα πιστοποίησης. Μη χρησιμοποιείς κωδικούς κριτηρίων — φυσική γλώσσα. Έκταση ~150 λέξεις ανά γλώσσα.";

                string userMsg =
                    "Κατάλυμα: " + hotelName + ", κατηγορία " + hotel.category + ", " +
                    hotel.totalRooms + " δωμάτια.\n" +
                    "Πιστοποίηση: βαθμίδα " + medalTitle + " (βαθμολογία " + (v3.totalScore ?? 0).ToString("0.##") + "/95) " +
                    "στο Σύστημα Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων (ΞΕΕ).\n\n" +
                    "ΠΙΣΤΟΠΟΙΗΜΕΝΑ ΣΤΟΙΧΕΙΑ (κωδικός|τίτλος|πυλώνας|απάντηση):\n" + lines;

                string reply = Utils.AiService.Chat(system, userMsg, 0.5m, 1400);
                if (string.IsNullOrEmpty(reply))
                    return Ok(new { success = false, message = "Το μοντέλο δεν απάντησε — δοκιμάστε ξανά." });

                return Ok(new { success = true, text = reply });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.GreenProfile");
                return Ok(new { success = false, message = "Σφάλμα — δοκιμάστε ξανά." });
            }
        }

        // ── #6: Σημασιολογική αναζήτηση κριτηρίων ───────────────────────
        // Embedding του query → cosine similarity με τα cached embeddings των
        // ενεργών κριτηρίων (lazy build/refresh με content hash) → top 6.
        [Route("api/AiApi/SearchCriteria")]
        [HttpPost]
        public IHttpActionResult SearchCriteria([FromBody] AiSearchRequest req)
        {
            try
            {
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο." });
                if (req == null || string.IsNullOrWhiteSpace(req.query))
                    return Ok(new { success = false, message = "Κενή αναζήτηση." });

                string query = req.query.Trim();
                if (query.Length > 300) query = query.Substring(0, 300);

                // Ενεργά κριτήρια + δομή για τίτλους πυλώνων
                List<Criteria> crits = unitOfWork.CriteriaRepository
                    .Get(c => c.dateFrom <= DateTime.Now && c.dateTo >= DateTime.Now).ToList();
                List<CategoryViewModel> cats = unitOfWork.context.Database
                    .SqlQuery<CategoryViewModel>("Select * from V_TEE_Categories where isActive=1").ToList();
                Dictionary<decimal, CategoryViewModel> catById = cats.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());

                // Cache embeddings + εντοπισμός ελλειπόντων/stale (MD5 τίτλου+περιγραφής)
                var cache = unitOfWork.AiCriteriaEmbeddingRepository.Get().ToList()
                    .GroupBy(x => x.criteriaID).ToDictionary(g => g.Key, g => g.First());

                Func<Criteria, string> contentOf = c =>
                    (c.code ?? "") + " " + (c.title ?? "") + "\n" + (c.description ?? "");

                var stale = new List<Criteria>();
                foreach (var c in crits)
                {
                    string hash = Utils.Encryptor.MD5Hash(contentOf(c));
                    AiCriteriaEmbedding e;
                    if (!cache.TryGetValue(c.id, out e) || e.contentHash != hash)
                        stale.Add(c);
                }

                // Batch υπολογισμός για όσα λείπουν (μία κλήση για όλα)
                if (stale.Count > 0)
                {
                    var vectors = Utils.AiService.EmbedBatch(stale.Select(contentOf).ToList());
                    if (vectors == null || vectors.Count != stale.Count)
                        return Ok(new { success = false, message = "Σφάλμα υπολογισμού embeddings — δείτε το TEE_ErrorLog." });

                    for (int i = 0; i < stale.Count; i++)
                    {
                        string hash = Utils.Encryptor.MD5Hash(contentOf(stale[i]));
                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(vectors[i]);
                        AiCriteriaEmbedding e;
                        if (cache.TryGetValue(stale[i].id, out e))
                        {
                            e.contentHash = hash; e.embedding = json; e.updatedDateTime = DateTime.Now;
                            unitOfWork.AiCriteriaEmbeddingRepository.Update(e);
                        }
                        else
                        {
                            e = new AiCriteriaEmbedding
                            {
                                criteriaID = stale[i].id, contentHash = hash,
                                embedding = json, updatedDateTime = DateTime.Now
                            };
                            unitOfWork.AiCriteriaEmbeddingRepository.Insert(e);
                            cache[stale[i].id] = e;
                        }
                    }
                    unitOfWork.Save();
                }

                // Embedding του query
                var qVecs = Utils.AiService.EmbedBatch(new List<string> { query });
                if (qVecs == null || qVecs.Count == 0)
                    return Ok(new { success = false, message = "Σφάλμα αναζήτησης — δοκιμάστε ξανά." });
                float[] qv = qVecs[0];

                // Cosine ranking
                var scored = new List<Tuple<Criteria, double>>();
                foreach (var c in crits)
                {
                    AiCriteriaEmbedding e;
                    if (!cache.TryGetValue(c.id, out e)) continue;
                    float[] v = Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(e.embedding);
                    scored.Add(Tuple.Create(c, Utils.AiService.Cosine(qv, v)));
                }

                var top = scored.OrderByDescending(x => x.Item2).Take(6)
                    .Where(x => x.Item2 > 0.15)   // κόφτης εντελώς άσχετων
                    .Select(x =>
                    {
                        CategoryViewModel sub; catById.TryGetValue(x.Item1.categoryID, out sub);
                        CategoryViewModel pillar = null;
                        if (sub != null && sub.parentID.HasValue) catById.TryGetValue(sub.parentID.Value, out pillar);
                        return new
                        {
                            id = x.Item1.id,
                            code = x.Item1.code,
                            title = x.Item1.title,
                            pillar = pillar != null ? pillar.title : "",
                            pillarID = pillar != null ? (decimal?)pillar.id : null,
                            subID = x.Item1.categoryID,
                            score = Math.Round(x.Item2 * 100)
                        };
                    }).ToList();

                return Ok(new { success = true, results = top });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.SearchCriteria");
                return Ok(new { success = false, message = "Σφάλμα αναζήτησης." });
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
