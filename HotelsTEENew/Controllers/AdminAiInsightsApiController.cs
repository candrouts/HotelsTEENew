using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    public class InsightsRequest
    {
        public int days { get; set; }              // 7 | 30 | 90
        public int batchSize { get; set; }         // για BatchCheckNext
        public decimal inspectorID { get; set; }   // για GetInspectorFindings
    }

    // AI Insights (admin): στατιστικά χρήσης, AI ανάλυση θεμάτων chat,
    // batch έλεγχος τεκμηρίων τελικών κατατάξεων (v3) & scorecard επιθεωρητών.
    [Authorize]
    public class AdminAiInsightsApiController : ApiController
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

        private static int NormDays(int d) { return (d == 7 || d == 30 || d == 90) ? d : 30; }

        // Τελικές κατατάξεις (v3, υποβεβλημένες) — η «υπογραφή» του επιθεωρητή
        private List<HotelCriteria> GetSubmittedFinals()
        {
            return unitOfWork.HotelCriteriaRepository
                .Get(x => x.version == 3 && x.status == 2 && x.certificateID.HasValue)
                .ToList();
        }

        // Τελευταίος έλεγχος ανά τεκμήριο
        private Dictionary<decimal, AiDocumentCheck> LatestChecks(List<decimal> fileIds)
        {
            if (fileIds.Count == 0) return new Dictionary<decimal, AiDocumentCheck>();
            return unitOfWork.AiDocumentCheckRepository
                .Get(x => fileIds.Contains(x.hotelCriteriaFileID)).ToList()
                .GroupBy(x => x.hotelCriteriaFileID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.checkedDateTime).First());
        }

        [Route("api/AdminAiInsightsApi/GetInsights")]
        [HttpPost]
        public IHttpActionResult GetInsights([FromBody] InsightsRequest req)
        {
            try
            {
                if (!IsAdmin()) return Ok(new { success = false });
                int days = NormDays(req != null ? req.days : 30);
                DateTime cutoff = DateTime.Today.AddDays(-days);

                // ── 1. Χρήση AI Συμβούλου ────────────────────────────────
                var chats = unitOfWork.AiChatLogRepository.Get(x => x.logDateTime >= cutoff).ToList();
                var chatStats = new
                {
                    totalQuestions = chats.Count,
                    distinctUsers = chats.Select(x => x.userName).Distinct().Count(),
                    avgPerUser = chats.Count == 0 ? 0 :
                        Math.Round((double)chats.Count / Math.Max(1, chats.Select(x => x.userName).Distinct().Count()), 1)
                };

                // ── Scorecard επιθεωρητών (v3 υποβεβλημένες) ─────────────
                List<HotelCriteria> finals = GetSubmittedFinals();
                List<decimal> certIds = finals.Select(f => f.certificateID.Value).Distinct().ToList();
                List<HotelierCertificate> certs = certIds.Count == 0 ? new List<HotelierCertificate>() :
                    unitOfWork.HotelierCertificateRepository.Get(x => certIds.Contains(x.certificateID)).ToList();
                Dictionary<decimal, decimal?> inspByCert = certs.ToDictionary(c => c.certificateID, c => c.tee_inspectorID);

                List<decimal> v3Ids = finals.Select(f => f.id).ToList();
                List<HotelCriteria_CriteriaFile> files = v3Ids.Count == 0 ? new List<HotelCriteria_CriteriaFile>() :
                    unitOfWork.HotelCriteria_CriteriaFileRepository
                        .Get(x => v3Ids.Contains(x.hotelCriteriaID)).ToList();
                Dictionary<decimal, decimal> certByHcId = finals.ToDictionary(f => f.id, f => f.certificateID.Value);

                var latest = LatestChecks(files.Select(f => f.id).ToList());

                var inspectors = unitOfWork.InspectorRepository.Get().ToList()
                    .ToDictionary(i => i.id, i => i.firstName + " " + i.lastName);

                var scorecard = files
                    .GroupBy(f =>
                    {
                        decimal certId = certByHcId[f.hotelCriteriaID];
                        decimal? insp; inspByCert.TryGetValue(certId, out insp);
                        return insp ?? 0;
                    })
                    .Where(g => g.Key != 0)
                    .Select(g =>
                    {
                        var checks = g.Where(f => latest.ContainsKey(f.id)).Select(f => latest[f.id]).ToList();
                        int checkedCount = checks.Count;
                        int fail = checks.Count(c => c.verdict == "fail");
                        int warn = checks.Count(c => c.verdict == "warn");
                        int okC = checks.Count(c => c.verdict == "ok");
                        int contradicts = checks.Count(c => c.answerVerdict == "contradicts");
                        int supported = checks.Count(c => c.answerVerdict == "supported");
                        double issueRate = checkedCount == 0 ? 0 : (double)(fail + contradicts) / checkedCount * 100;

                        string rating = checkedCount < 15 ? "insufficient"
                            : issueRate < 5 ? "green" : issueRate < 15 ? "yellow" : "red";

                        return new
                        {
                            inspectorID = g.Key,
                            inspectorName = inspectors.ContainsKey(g.Key) ? inspectors[g.Key] : ("#" + g.Key),
                            applications = g.Select(f => certByHcId[f.hotelCriteriaID]).Distinct().Count(),
                            totalFiles = g.Count(),
                            checkedFiles = checkedCount,
                            uncheckedFiles = g.Count() - checkedCount,
                            ok = okC, warn = warn, fail = fail,
                            supported = supported, contradicts = contradicts,
                            issueRate = Math.Round(issueRate, 1),
                            rating = rating
                        };
                    })
                    .OrderByDescending(x => x.issueRate).ToList();

                int pendingBatch = files.Count(f => !latest.ContainsKey(f.id));

                // ── Ποιότητα τεκμηρίωσης ανά κριτήριο ────────────────────
                var cfIds = files.Select(f => f.criteriaFileID).Distinct().ToList();
                var cfs = cfIds.Count == 0 ? new List<Criteria_File>() :
                    unitOfWork.Criteria_FileRepository.Get(x => cfIds.Contains(x.id)).ToList();
                var critIds = cfs.Select(c => c.criteriaID).Distinct().ToList();
                var crits = critIds.Count == 0 ? new List<Criteria>() :
                    unitOfWork.CriteriaRepository.Get(x => critIds.Contains(x.id)).ToList();
                var cfById = cfs.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());
                var critById = crits.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());

                var criteriaQuality = files
                    .Where(f => latest.ContainsKey(f.id) && cfById.ContainsKey(f.criteriaFileID))
                    .GroupBy(f => cfById[f.criteriaFileID].criteriaID)
                    .Select(g =>
                    {
                        var checks = g.Select(f => latest[f.id]).ToList();
                        int issues = checks.Count(c => c.verdict != "ok" || c.answerVerdict == "contradicts");
                        Criteria cr; critById.TryGetValue(g.Key, out cr);
                        return new
                        {
                            code = cr != null ? cr.code : ("#" + g.Key),
                            title = cr != null ? cr.title : "",
                            total = checks.Count,
                            issues = issues,
                            issueRate = Math.Round((double)issues / checks.Count * 100, 0)
                        };
                    })
                    .Where(x => x.issues > 0)
                    .OrderByDescending(x => x.issueRate).ThenByDescending(x => x.total)
                    .Take(10).ToList();

                // ── Πρόσφατες συνομιλίες ────────────────────────────────
                var recentChats = unitOfWork.AiChatLogRepository.Get().ToList()
                    .OrderByDescending(x => x.logDateTime).Take(20)
                    .Select(x => new
                    {
                        user = x.userName,
                        date = x.logDateTime.ToString("dd/MM HH:mm"),
                        question = x.question,
                        answer = x.answer != null && x.answer.Length > 400 ? x.answer.Substring(0, 400) + "…" : x.answer
                    }).ToList();

                // ── Cached AI ανάλυση θεμάτων ───────────────────────────
                var cached = unitOfWork.AiInsightsReportRepository
                    .Get(x => x.periodDays == days).ToList()
                    .OrderByDescending(x => x.createdDateTime).FirstOrDefault();

                return Ok(new
                {
                    success = true,
                    days = days,
                    chatStats = chatStats,
                    docStats = new
                    {
                        totalChecked = latest.Count,
                        pendingBatch = pendingBatch,
                        ok = latest.Values.Count(c => c.verdict == "ok"),
                        warn = latest.Values.Count(c => c.verdict == "warn"),
                        fail = latest.Values.Count(c => c.verdict == "fail"),
                        contradicts = latest.Values.Count(c => c.answerVerdict == "contradicts")
                    },
                    scorecard = scorecard,
                    criteriaQuality = criteriaQuality,
                    recentChats = recentChats,
                    cachedReport = cached != null ? cached.reportText : null,
                    cachedReportDate = cached != null ? cached.createdDateTime.ToString("dd/MM/yyyy HH:mm") : null
                });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AdminAiInsightsApi.GetInsights");
                return Ok(new { success = false });
            }
        }

        // AI ανάλυση θεμάτων των συνομιλιών της περιόδου (on-demand + cache)
        [Route("api/AdminAiInsightsApi/AnalyzeChats")]
        [HttpPost]
        public IHttpActionResult AnalyzeChats([FromBody] InsightsRequest req)
        {
            try
            {
                if (!IsAdmin()) return Ok(new { success = false });
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο." });

                int days = NormDays(req != null ? req.days : 30);
                DateTime cutoff = DateTime.Today.AddDays(-days);

                var chats = unitOfWork.AiChatLogRepository.Get(x => x.logDateTime >= cutoff).ToList()
                    .OrderByDescending(x => x.logDateTime).Take(150).ToList();
                if (chats.Count == 0)
                    return Ok(new { success = false, message = "Δεν υπάρχουν συνομιλίες στην περίοδο." });

                var lines = chats.Select(c =>
                {
                    string q = c.question ?? "";
                    string a = c.answer ?? "";
                    if (q.Length > 300) q = q.Substring(0, 300);
                    if (a.Length > 300) a = a.Substring(0, 300);
                    return "Ε: " + q + "\nΑ: " + a;
                });

                string system =
                    "Είσαι αναλυτής προϊόντος για το Σύστημα Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων. " +
                    "Αναλύεις ερωταπαντήσεις ξενοδόχων με τον AI Σύμβουλο και συντάσσεις σύντομη αναφορά στα ελληνικά με ΑΚΡΙΒΩΣ αυτή τη δομή:\n" +
                    "ΚΟΡΥΦΑΙΑ ΘΕΜΑΤΑ: (λίστα με εκτίμηση ποσοστού)\n" +
                    "ΣΗΜΕΙΑ ΣΥΓΧΥΣΗΣ ΧΡΗΣΤΩΝ: (τι δυσκολεύει/μπερδεύει τους ξενοδόχους)\n" +
                    "ΑΝΑΠΑΝΤΗΤΕΣ/ΑΔΥΝΑΜΕΣ ΑΠΑΝΤΗΣΕΙΣ: (πού ο Σύμβουλος δεν βοήθησε — κενά γνώσης του)\n" +
                    "ΠΡΟΤΕΙΝΟΜΕΝΕΣ ΕΝΕΡΓΕΙΕΣ: (3-5 συγκεκριμένες, υλοποιήσιμες προτάσεις)\n" +
                    "Βασίσου ΜΟΝΟ στα δεδομένα. Έκταση έως ~300 λέξεις.";

                string user = "Ερωταπαντήσεις περιόδου (" + days + " ημέρες, " + chats.Count + " καταχωρίσεις):\n\n" +
                              string.Join("\n---\n", lines);

                string report = Utils.AiService.Chat(system, user, 0.3m, 1200);
                if (string.IsNullOrEmpty(report))
                    return Ok(new { success = false, message = "Το μοντέλο δεν απάντησε — δοκιμάστε ξανά." });

                unitOfWork.AiInsightsReportRepository.Insert(new AiInsightsReport
                {
                    periodDays = days,
                    reportText = report,
                    createdBy = User.Identity.Name,
                    createdDateTime = DateTime.Now
                });
                unitOfWork.Save();

                return Ok(new { success = true, report = report, date = DateTime.Now.ToString("dd/MM/yyyy HH:mm") });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AdminAiInsightsApi.AnalyzeChats");
                return Ok(new { success = false, message = "Σφάλμα ανάλυσης." });
            }
        }

        // Batch: έλεγχος των επόμενων Ν ανέλεγκτων τεκμηρίων τελικών κατατάξεων (v3).
        // Καλείται επαναληπτικά από το UI με progress.
        [Route("api/AdminAiInsightsApi/BatchCheckNext")]
        [HttpPost]
        public IHttpActionResult BatchCheckNext([FromBody] InsightsRequest req)
        {
            try
            {
                if (!IsAdmin()) return Ok(new { success = false });
                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο." });

                int batch = req != null && req.batchSize > 0 ? Math.Min(req.batchSize, 5) : 3;

                List<HotelCriteria> finals = GetSubmittedFinals();
                List<decimal> v3Ids = finals.Select(f => f.id).ToList();
                Dictionary<decimal, HotelCriteria> hcById = finals.ToDictionary(f => f.id, f => f);

                List<HotelCriteria_CriteriaFile> files = v3Ids.Count == 0 ? new List<HotelCriteria_CriteriaFile>() :
                    unitOfWork.HotelCriteria_CriteriaFileRepository
                        .Get(x => v3Ids.Contains(x.hotelCriteriaID)).ToList();

                var checkedIds = new HashSet<decimal>(
                    files.Count == 0 ? new List<decimal>() :
                    unitOfWork.AiDocumentCheckRepository.Get().Select(x => x.hotelCriteriaFileID).Distinct().ToList());

                var pending = files.Where(f => !checkedIds.Contains(f.id)).OrderBy(f => f.id).ToList();
                int remainingBefore = pending.Count;
                if (remainingBefore == 0)
                    return Ok(new { success = true, processed = 0, remaining = 0, done = true });

                var results = new List<object>();
                foreach (var f in pending.Take(batch))
                {
                    HotelCriteria hc = hcById[f.hotelCriteriaID];
                    var outcome = Utils.AiDocumentChecker.Run(unitOfWork, f, hc, "batch:" + User.Identity.Name);
                    results.Add(new
                    {
                        fileID = f.id,
                        fileName = f.fileName,
                        success = outcome.success,
                        verdict = outcome.verdict,
                        answerVerdict = outcome.answerVerdict,
                        message = outcome.message
                    });

                    // Αποτυχία (π.χ. αρχείο λείπει): καταγραφή ως 'warn' ώστε να μην ξανα-επιχειρείται αενάως
                    if (!outcome.success)
                    {
                        try
                        {
                            unitOfWork.AiDocumentCheckRepository.Insert(new AiDocumentCheck
                            {
                                hotelCriteriaFileID = f.id,
                                verdict = "warn",
                                answerVerdict = "na",
                                summary = "Ο αυτόματος έλεγχος απέτυχε: " + (outcome.message ?? "άγνωστο σφάλμα"),
                                model = "batch-error",
                                checkedBy = "batch:" + User.Identity.Name,
                                checkedDateTime = DateTime.Now
                            });
                            unitOfWork.Save();
                        }
                        catch (Exception exIns) { HotelsTEE.Utils.ErrorLogger.Log(exIns, "AdminAiInsightsApi.BatchCheckNext.MarkFailed"); }
                    }
                }

                int processed = Math.Min(batch, remainingBefore);
                return Ok(new
                {
                    success = true,
                    processed = processed,
                    remaining = remainingBefore - processed,
                    done = remainingBefore - processed <= 0,
                    results = results
                });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AdminAiInsightsApi.BatchCheckNext");
                return Ok(new { success = false, message = "Σφάλμα batch ελέγχου." });
            }
        }

        // Drill-down: ευρήματα (warn/fail/contradicts) ενός επιθεωρητή
        [Route("api/AdminAiInsightsApi/GetInspectorFindings")]
        [HttpPost]
        public IHttpActionResult GetInspectorFindings([FromBody] InsightsRequest req)
        {
            try
            {
                if (!IsAdmin() || req == null || req.inspectorID <= 0) return Ok(new { success = false });

                decimal inspID = req.inspectorID;
                List<decimal> certIds = unitOfWork.HotelierCertificateRepository
                    .Get(x => x.tee_inspectorID == inspID && x.certificateTypeID == 84)
                    .Select(x => x.certificateID).ToList();

                List<HotelCriteria> finals = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.version == 3 && x.status == 2 && x.certificateID.HasValue && certIds.Contains(x.certificateID.Value))
                    .ToList();
                List<decimal> v3Ids = finals.Select(f => f.id).ToList();
                Dictionary<decimal, decimal> certByHc = finals.ToDictionary(f => f.id, f => f.certificateID.Value);

                List<HotelCriteria_CriteriaFile> files = v3Ids.Count == 0 ? new List<HotelCriteria_CriteriaFile>() :
                    unitOfWork.HotelCriteria_CriteriaFileRepository.Get(x => v3Ids.Contains(x.hotelCriteriaID)).ToList();
                var fileById = files.ToDictionary(f => f.id, f => f);

                var latest = LatestChecks(files.Select(f => f.id).ToList());

                // Τίτλοι καταλυμάτων + κριτήρια για τα ευρήματα
                var cfIds = files.Select(f => f.criteriaFileID).Distinct().ToList();
                var cfs = cfIds.Count == 0 ? new List<Criteria_File>() :
                    unitOfWork.Criteria_FileRepository.Get(x => cfIds.Contains(x.id)).ToList();
                var cfById = cfs.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());
                var critIds = cfs.Select(c => c.criteriaID).Distinct().ToList();
                var crits = critIds.Count == 0 ? new List<Criteria>() :
                    unitOfWork.CriteriaRepository.Get(x => critIds.Contains(x.id)).ToList();
                var critById = crits.GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());

                var findings = latest.Values
                    .Where(c => c.verdict != "ok" || c.answerVerdict == "contradicts")
                    .OrderByDescending(c => c.verdict == "fail" ? 2 : c.answerVerdict == "contradicts" ? 2 : 1)
                    .ThenByDescending(c => c.checkedDateTime)
                    .Take(50)
                    .Select(c =>
                    {
                        HotelCriteria_CriteriaFile f = fileById[c.hotelCriteriaFileID];
                        Criteria_File cf; cfById.TryGetValue(f.criteriaFileID, out cf);
                        Criteria cr = null;
                        if (cf != null) critById.TryGetValue(cf.criteriaID, out cr);
                        return new
                        {
                            certificateID = certByHc[f.hotelCriteriaID],
                            criterion = cr != null ? cr.code : "-",
                            docTitle = cf != null ? cf.title : "-",
                            fileName = f.fileName,
                            verdict = c.verdict,
                            answerVerdict = c.answerVerdict,
                            summary = c.summary
                        };
                    }).ToList();

                return Ok(new { success = true, findings = findings });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AdminAiInsightsApi.GetInspectorFindings");
                return Ok(new { success = false });
            }
        }
    }
}
