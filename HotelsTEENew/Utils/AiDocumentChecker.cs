using HotelsTEE.DAL;
using HotelsTEE.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace HotelsTEE.Utils
{
    public class AiDocCheckOutcome
    {
        public bool success { get; set; }
        public string verdict { get; set; }
        public string answerVerdict { get; set; }
        public string summary { get; set; }
        public string message { get; set; }
    }

    // Πυρήνας AI προ-ελέγχου τεκμηρίου: λήψη αρχείου → OCR → κρίση GPT →
    // αποθήκευση στο TEE_AI_DocumentCheck. Καλείται από τον χειροκίνητο
    // έλεγχο του επιθεωρητή ΚΑΙ από το admin batch (AI Insights).
    public static class AiDocumentChecker
    {
        public static AiDocCheckOutcome Run(UnitOfWork unitOfWork, HotelCriteria_CriteriaFile file, HotelCriteria hc, string checkedBy)
        {
            var res = new AiDocCheckOutcome { success = false };
            try
            {
                // Πλαίσιο: απαιτούμενο τεκμήριο + κριτήριο + κατάλυμα
                Criteria_File cf = unitOfWork.Criteria_FileRepository.GetByID(file.criteriaFileID);
                Criteria crit = cf != null ? unitOfWork.CriteriaRepository.GetByID(cf.criteriaID) : null;

                // Η δηλωθείσα απάντηση στο κριτήριο (ίδια έκδοση αξιολόγησης)
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

                string options = "";
                if (crit != null && !string.IsNullOrEmpty(crit.selectList)) options = crit.selectList;
                else if (crit != null && !string.IsNullOrEmpty(crit.gradesOptions)) options = crit.gradesOptions;
                if (options.Length > 800) options = options.Substring(0, 800);

                string methodology = crit != null ? (crit.description ?? "") : "";
                if (methodology.Length > 2500) methodology = methodology.Substring(0, 2500);

                string aiInstructions = "";
                if (crit != null)
                {
                    decimal cid = crit.id;
                    AiCriteriaInstruction instr = unitOfWork.AiCriteriaInstructionRepository
                        .Get(x => x.criteriaID == cid).FirstOrDefault();
                    if (instr != null) aiInstructions = instr.instructions ?? "";
                }

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
                    if (s == null) { res.message = "Το αρχείο δεν βρέθηκε στον αποθηκευτικό χώρο."; return res; }
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

                string text = AiService.ExtractText(bytes, contentType);
                if (string.IsNullOrWhiteSpace(text)) { res.message = "Δεν ήταν δυνατή η ανάγνωση του εγγράφου (OCR)."; return res; }
                if (text.Length > 12000) text = text.Substring(0, 12000);

                string system =
                    "Είσαι βοηθός επιθεωρητή στο Σύστημα Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων. " +
                    "Ελέγχεις ένα μεταφορτωμένο τεκμήριο σε ΔΥΟ επίπεδα και απαντάς ΑΥΣΤΗΡΑ με JSON " +
                    "{\"typeVerdict\":\"ok|warn|fail\",\"answerVerdict\":\"supported|unclear|contradicts|na\",\"summary\":\"...\"} χωρίς άλλο κείμενο.\n" +
                    "1) typeVerdict — καταλληλότητα είδους: ok=ανταποκρίνεται στο ζητούμενο τεκμήριο, warn=σχετικό με επιφυλάξεις (ελλιπές, παλιό), fail=άσχετο/ακατάλληλο.\n" +
                    "2) answerVerdict — αν το τεκμήριο υποστηρίζει τη ΔΗΛΩΘΕΙΣΑ ΑΠΑΝΤΗΣΗ στο κριτήριο: " +
                    "supported=τα στοιχεία του εγγράφου επιβεβαιώνουν τη δήλωση, " +
                    "unclear=δεν επαρκούν τα στοιχεία, " +
                    "contradicts=τα στοιχεία ΑΝΤΙΚΡΟΥΟΥΝ τη δήλωση με βεβαιότητα, " +
                    "na=δεν έχει νόημα η σύγκριση (π.χ. δεν υπάρχει απάντηση ή το έγγραφο είναι άσχετο).\n" +
                    "ΓΕΝΙΚΟΙ ΚΑΝΟΝΕΣ:\n" +
                    "- Αν χρειάζεται μετατροπή μονάδων ή υπολογισμός, ΚΑΝΕ τον και δείξε τον ΑΝΑΛΥΤΙΚΑ στο summary (π.χ. «53 L/κύκλο ÷ 6 kg = 8,8 L/kg < 10 ✔»).\n" +
                    "- Όταν το έγγραφο δίνει μετρήσεις για πολλά προγράμματα/καταστάσεις λειτουργίας, χρησιμοποίησε το ΤΥΠΙΚΟ/ΠΡΟΤΥΠΟ πρόγραμμα μέτρησης σε πλήρες ονομαστικό φορτίο (π.χ. για πλυντήρια το πρόγραμμα αναφοράς ΕΕ)· αν δεν υπάρχει, χρησιμοποίησε το κύριο πρόγραμμα κανονικής χρήσης. Δείξε στο summary ποιο επέλεξες. ΜΗΝ βασίζεις την κρίση σε βοηθητικά/ακραία προγράμματα (σύντομης πλύσης, οικονομίας χρόνου κ.λπ.).\n" +
                    "- ΜΗΝ απαιτείς το τεκμήριο να αναφέρει την επωνυμία του καταλύματος όταν πρόκειται για τεχνικό φυλλάδιο/προδιαγραφές κατασκευαστή — αυτά εκ φύσεως δεν την περιέχουν.\n" +
                    "- ΜΗΝ ζητάς απόδειξη για το πώς χρησιμοποιείται ο εξοπλισμός στην πράξη — η αξιολόγηση γίνεται βάσει των ονομαστικών προδιαγραφών.\n" +
                    "- contradicts ΜΟΝΟ όταν ο υπολογισμός είναι αδιαμφισβήτητος. unclear μόνο όταν πραγματικά λείπουν στοιχεία που δεν καλύπτονται από τους παραπάνω κανόνες.\n" +
                    (aiInstructions.Length > 0
                        ? "ΕΙΔΙΚΕΣ ΟΔΗΓΙΕΣ ΓΙΑ ΤΟ ΣΥΓΚΕΚΡΙΜΕΝΟ ΚΡΙΤΗΡΙΟ (κατευθύνσεις προτεραιότητας — εφάρμοσέ τες όπου έχουν εφαρμογή· αν το έγγραφο δεν περιέχει τα στοιχεία που αναφέρουν, χρησιμοποίησε τα πλησιέστερα ισοδύναμα διαθέσιμα στοιχεία και σημείωσέ το, ΜΗΝ απορρίπτεις το τεκμήριο γι' αυτό): " + aiInstructions + "\n"
                        : "") +
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

                string reply = AiService.Chat(system, user, 0m, 600);
                if (string.IsNullOrEmpty(reply)) { res.message = "Το μοντέλο δεν απάντησε — δείτε το TEE_ErrorLog."; return res; }

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
                    checkedBy = checkedBy,
                    checkedDateTime = DateTime.Now
                };
                unitOfWork.AiDocumentCheckRepository.Insert(check);
                unitOfWork.Save();

                res.success = true;
                res.verdict = verdict;
                res.answerVerdict = answerVerdict;
                res.summary = summary;
                return res;
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "AiDocumentChecker.Run");
                res.message = "Σφάλμα ελέγχου — δείτε το TEE_ErrorLog.";
                return res;
            }
        }
    }
}
