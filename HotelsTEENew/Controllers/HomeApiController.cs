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
    [Authorize]
    public class HomeApiController : ApiController
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        // Dashboard ξενοδόχου: βαθμολογία ανά κατηγορία/υποκατηγορία
        // σε σύγκριση με τον Μ.Ο. των υπολοίπων ξενοδοχείων (με φίλτρα).
        [Route("api/HomeApi/GetDashboard")]
        [HttpPost]
        public IHttpActionResult GetDashboard([FromBody] DashboardFilterViewModel filter)
        {
            DashboardViewModel result = new DashboardViewModel
            {
                isHotelier = false,
                categories = new List<DashboardItemViewModel>(),
                subCategories = new List<DashboardItemViewModel>(),
                hotelCategories = new List<DashboardFilterOptionViewModel>(),
                peripheries = new List<DashboardFilterOptionViewModel>(),
                periferiakesEnotites = new List<DashboardFilterOptionViewModel>()
            };

            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                if (user == null || (user.role != 1 && user.role != 100))
                    return Ok(result);   // ούτε ξενοδόχος ούτε admin

                bool isAdmin = user.role == 100;
                result.isAdmin = isAdmin;
                result.isHotelier = user.role == 1;

                // Admin με επιλεγμένο ξενοδοχείο: τα charts όπως τα βλέπει ο ξενοδόχος
                bool viewAsHotel = isAdmin
                                   && !string.IsNullOrEmpty(filter?.targetHotelID)
                                   && !string.IsNullOrEmpty(filter?.targetCompanyID);
                result.viewAsHotel = viewAsHotel;

                HotelDetailsViewModel hotelDetails = null;
                HotelCriteriaViewModel mySelf = null;
                HotelCriteriaViewModel myFinal = null;

                if (!isAdmin)
                {
                    sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
                    hotelDetails = unitOfWork.context.Database
                        .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                        .FirstOrDefault();
                    if (hotelDetails == null)
                        return Ok(result);
                }
                else if (viewAsHotel)
                {
                    sql = "Select * from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                    hotelDetails = unitOfWork.context.Database
                        .SqlQuery<HotelDetailsViewModel>(sql,
                            new SqlParameter("@hotelID", filter.targetHotelID),
                            new SqlParameter("@companyID", filter.targetCompanyID))
                        .FirstOrDefault();

                    if (hotelDetails == null)
                    {
                        result.viewAsHotel = viewAsHotel = false;   // άγνωστο ξενοδοχείο → καθαρά admin view
                    }
                    else
                    {
                        result.targetHotelTitle = hotelDetails.hotelTitle;
                    }
                }

                // Εκδόσεις του ξενοδοχείου (δικού του ή επιλεγμένου από admin)
                if (hotelDetails != null)
                {
                    sql = "Select * from V_TEE_HotelCriteria where hotelID = @hotelID AND exploitingCompanyID = @companyID and status=2 and version in (1,3)";
                    List<HotelCriteriaViewModel> myVersions = unitOfWork.context.Database
                        .SqlQuery<HotelCriteriaViewModel>(sql,
                            new SqlParameter("@hotelID", hotelDetails.hotelID),
                            new SqlParameter("@companyID", hotelDetails.exploitingCompanyID))
                        .ToList();

                    // Προτεραιότητα στον ΕΝΕΡΓΟ κύκλο (isFinished=0), αλλιώς ο πιο πρόσφατος ολοκληρωμένος
                    mySelf = myVersions.Where(x => x.version == 1)
                        .OrderBy(x => x.isFinished ? 1 : 0).ThenByDescending(x => x.id).FirstOrDefault();
                    myFinal = myVersions.Where(x => x.version == 3)
                        .OrderBy(x => x.isFinished ? 1 : 0).ThenByDescending(x => x.id).FirstOrDefault();
                }

                bool hotelierMode = !isAdmin || viewAsHotel;

                result.hasSubmitted = !hotelierMode || mySelf != null;
                result.hasFinal = (isAdmin && !viewAsHotel) || myFinal != null;

                // ── Στάδιο σύγκρισης: 1 = Αυτοαξιολόγηση, 3 = Τελική Κατάταξη ──
                // Default σε hotelier mode: το πιο ώριμο διαθέσιμο στάδιο | καθαρός admin: Αυτοαξιολόγηση
                int stage;
                if (filter != null && (filter.version == 1 || filter.version == 3))
                    stage = filter.version.Value;
                else
                    stage = (hotelierMode && myFinal != null) ? 3 : 1;

                result.stage = stage;

                HotelCriteriaViewModel myCriteria = stage == 3 ? myFinal : mySelf;

                // ── Κατηγορίες (δομή δένδρου) ────────────────────────────────
                sql = "Select * from V_TEE_Categories where isActive=1 order by [order]";
                List<CategoryViewModel> allCategories = unitOfWork.context.Database.SqlQuery<CategoryViewModel>(sql).ToList();

                // ── Λίστες φίλτρων ──────────────────────────────────────────
                sql = @"Select distinct category as value, category as title from V_TEE_HotelDetails
                        where category is not null and category <> '' order by category";
                result.hotelCategories = unitOfWork.context.Database.SqlQuery<DashboardFilterOptionViewModel>(sql).ToList();

                // Περιφέρειες: μόνο όσες έχουν ξενοδοχεία, με τίτλο από ELSTATAreas
                sql = @"Select distinct hd.periphereiaID as value, ea.title as title
                        from V_TEE_HotelDetails hd
                        inner join ELSTATAreas ea on ea.kalID = hd.periphereiaID
                        where hd.periphereiaID is not null
                        order by ea.title";
                result.peripheries = unitOfWork.context.Database.SqlQuery<DashboardFilterOptionViewModel>(sql).ToList();

                // Περιφερειακές Ενότητες: της επιλεγμένης Περιφέρειας (αλλιώς όλες με ξενοδοχεία)
                string peWhere = "";
                List<SqlParameter> peParams = new List<SqlParameter>();
                if (!string.IsNullOrEmpty(filter?.periphereiaID))
                {
                    peWhere = " and hd.periphereiaID = @periphereiaID ";
                    peParams.Add(new SqlParameter("@periphereiaID", filter.periphereiaID));
                }
                sql = @"Select distinct hd.peripheryID as value, ea.title as title
                        from V_TEE_HotelDetails hd
                        inner join ELSTATAreas ea on ea.kalID = hd.peripheryID
                        where hd.peripheryID is not null " + peWhere + @"
                        order by ea.title";
                result.periferiakesEnotites = unitOfWork.context.Database
                    .SqlQuery<DashboardFilterOptionViewModel>(sql, peParams.Cast<object>().ToArray())
                    .ToList();

                // ── Δικοί του πόντοι ανά υποκατηγορία ───────────────────────
                List<DashboardScoreRowViewModel> myRows = new List<DashboardScoreRowViewModel>();
                if (myCriteria != null)
                {
                    sql = @"
                        SELECT hcc.hotelCriteriaID, c.categoryID,
                               SUM(ISNULL(hcc.points,0)) AS points,
                               SUM(CASE
                                       WHEN c.criteriaType = 3 THEN CASE WHEN hcc.isChecked = 1 THEN ISNULL(c.weight,0) * ISNULL(c.maxGrade,0) ELSE 0 END
                                       WHEN ISNULL(hcc.isApplicable,1) = 1 THEN ISNULL(c.weight,0) * ISNULL(c.maxGrade,0)
                                       ELSE 0
                                   END) AS maxPoints
                        FROM V_TEE_HotelCriteria_Criteria hcc
                        INNER JOIN V_TEE_Criteria c ON c.id = hcc.criteriaID
                        WHERE hcc.hotelCriteriaID = @hotelCriteriaID
                        GROUP BY hcc.hotelCriteriaID, c.categoryID";
                    myRows = unitOfWork.context.Database
                        .SqlQuery<DashboardScoreRowViewModel>(sql, new SqlParameter("@hotelCriteriaID", myCriteria.id))
                        .ToList();
                }

                // ── Πόντοι των υπολοίπων ξενοδοχείων (με φίλτρα) ────────────
                string othersWhere = "";
                List<SqlParameter> othersParams = new List<SqlParameter>();

                if (!string.IsNullOrEmpty(filter?.hotelCategory))
                {
                    othersWhere += " and hd.category = @hotelCategory ";
                    othersParams.Add(new SqlParameter("@hotelCategory", filter.hotelCategory));
                }
                if (!string.IsNullOrEmpty(filter?.periphereiaID))
                {
                    othersWhere += " and hd.periphereiaID = @perID ";
                    othersParams.Add(new SqlParameter("@perID", filter.periphereiaID));
                }
                if (!string.IsNullOrEmpty(filter?.peripheryID))
                {
                    othersWhere += " and hd.peripheryID = @peID ";
                    othersParams.Add(new SqlParameter("@peID", filter.peripheryID));
                }
                if (filter != null && filter.bedsFrom.HasValue)
                {
                    othersWhere += " and hd.totalBeds >= @bedsFrom ";
                    othersParams.Add(new SqlParameter("@bedsFrom", filter.bedsFrom.Value));
                }
                if (filter != null && filter.bedsTo.HasValue)
                {
                    othersWhere += " and hd.totalBeds <= @bedsTo ";
                    othersParams.Add(new SqlParameter("@bedsTo", filter.bedsTo.Value));
                }

                // Hotelier mode (ξενοδόχος ή admin με επιλεγμένο ξενοδοχείο):
                // το ξενοδοχείο-στόχος εξαιρείται από τον Μ.Ο. | Καθαρός admin: όλα τα ξενοδοχεία
                string excludeSelf = "";
                if (hotelierMode && hotelDetails != null)
                {
                    excludeSelf = " AND NOT (hc.hotelID = @myHotelID AND hc.exploitingCompanyID = @myCompanyID) ";
                    othersParams.Add(new SqlParameter("@myHotelID", hotelDetails.hotelID));
                    othersParams.Add(new SqlParameter("@myCompanyID", hotelDetails.exploitingCompanyID));
                }

                sql = @"
                    SELECT hcc.hotelCriteriaID, c.categoryID,
                           SUM(ISNULL(hcc.points,0)) AS points,
                           SUM(CASE
                                   WHEN c.criteriaType = 3 THEN CASE WHEN hcc.isChecked = 1 THEN ISNULL(c.weight,0) * ISNULL(c.maxGrade,0) ELSE 0 END
                                   WHEN ISNULL(hcc.isApplicable,1) = 1 THEN ISNULL(c.weight,0) * ISNULL(c.maxGrade,0)
                                   ELSE 0
                               END) AS maxPoints
                    FROM V_TEE_HotelCriteria hc
                    INNER JOIN V_TEE_HotelDetails hd
                        ON hd.hotelID = hc.hotelID AND hd.exploitingCompanyID = hc.exploitingCompanyID
                    INNER JOIN V_TEE_HotelCriteria_Criteria hcc ON hcc.hotelCriteriaID = hc.id
                    INNER JOIN V_TEE_Criteria c ON c.id = hcc.criteriaID
                    WHERE hc.version = @stage AND hc.status = 2
                      AND hc.id IN (
                          SELECT MAX(hc2.id) FROM V_TEE_HotelCriteria hc2
                          WHERE hc2.version = @stage AND hc2.status = 2
                          GROUP BY hc2.hotelID, hc2.exploitingCompanyID
                      )
                    " + excludeSelf + othersWhere + @"
                    GROUP BY hcc.hotelCriteriaID, c.categoryID";
                othersParams.Add(new SqlParameter("@stage", stage));
                List<DashboardScoreRowViewModel> otherRows = unitOfWork.context.Database
                    .SqlQuery<DashboardScoreRowViewModel>(sql, othersParams.Cast<object>().ToArray())
                    .ToList();

                int othersCount = otherRows.Select(x => x.hotelCriteriaID).Distinct().Count();
                result.othersCount = othersCount;

                // ── Aggregation σε C# ────────────────────────────────────────
                // map: υποκατηγορία -> κύρια κατηγορία
                Dictionary<decimal, decimal> subToParent = allCategories
                    .Where(x => x.parentID.HasValue)
                    .ToDictionary(x => x.id, x => x.parentID.Value);

                Func<List<DashboardScoreRowViewModel>, Dictionary<decimal, decimal>> sumPerSub = rows =>
                    rows.GroupBy(r => r.categoryID).ToDictionary(g => g.Key, g => g.Sum(r => r.points));

                Dictionary<decimal, decimal> mySub = sumPerSub(myRows);
                Dictionary<decimal, decimal> othersSubTotal = sumPerSub(otherRows);

                Func<Dictionary<decimal, decimal>, decimal, decimal> get = (d, k) => d.ContainsKey(k) ? d[k] : 0;

                // Υποκατηγορίες (πόντοι, χωρίς αναγωγή — όπως στη φόρμα)
                foreach (var sub in allCategories.Where(x => x.parentID.HasValue).OrderBy(x => x.order))
                {
                    result.subCategories.Add(new DashboardItemViewModel
                    {
                        id = sub.id,
                        parentID = sub.parentID,
                        title = sub.title,
                        myPoints = Math.Round(get(mySub, sub.id), 1),
                        avgPoints = othersCount > 0 ? Math.Round(get(othersSubTotal, sub.id) / othersCount, 1) : 0
                    });
                }

                // ── Κύριες κατηγορίες: αναγωγή ανά ξενοδοχείο ────────────────
                // norm = (raw / max) * totalUnits — όπως υπολογίζεται στη φόρμα κριτηρίων.

                // Δικά μου raw/max ανά κύρια κατηγορία
                Func<List<DashboardScoreRowViewModel>, decimal, decimal[]> rawMaxPerParent = (rows, parentId) =>
                {
                    var sel = rows.Where(r => subToParent.ContainsKey(r.categoryID) && subToParent[r.categoryID] == parentId);
                    return new[] { sel.Sum(r => r.points), sel.Sum(r => r.maxPoints) };
                };

                // Των υπολοίπων: ομαδοποίηση ανά ξενοδοχείο για αναγωγή ανά ξενοδοχείο
                var otherRowsPerHotel = otherRows.GroupBy(r => r.hotelCriteriaID).ToList();

                foreach (var cat in allCategories.Where(x => !x.parentID.HasValue).OrderBy(x => x.order))
                {
                    decimal totalUnits = cat.totalUnits ?? 0;

                    // Δικός μου
                    decimal[] mine = rawMaxPerParent(myRows, cat.id);
                    decimal myRaw = mine[0], myMax = mine[1];
                    decimal myNorm = myMax > 0 ? (myRaw / myMax) * totalUnits : 0;

                    // Μ.Ο. υπολοίπων: αναγωγή ανά ξενοδοχείο και μετά μέσος όρος
                    decimal sumNorm = 0, sumRaw = 0;
                    foreach (var hotelGroup in otherRowsPerHotel)
                    {
                        decimal[] h = rawMaxPerParent(hotelGroup.ToList(), cat.id);
                        sumRaw += h[0];
                        sumNorm += h[1] > 0 ? (h[0] / h[1]) * totalUnits : 0;
                    }

                    result.categories.Add(new DashboardItemViewModel
                    {
                        id = cat.id,
                        parentID = null,
                        title = cat.title,
                        myPoints = Math.Round(myNorm, 2),
                        avgPoints = othersCount > 0 ? Math.Round(sumNorm / othersCount, 2) : 0,
                        myRawPoints = Math.Round(myRaw, 1),
                        avgRawPoints = othersCount > 0 ? Math.Round(sumRaw / othersCount, 1) : 0
                    });
                }

                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                return Ok(result);
            }
        }

        // Επιστρέφει τα στοιχεία του logged-in ξενοδόχου ή null
        private HotelDetailsViewModel GetOwnHotel()
        {
            string sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
            return unitOfWork.context.Database
                .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
        }

        // ── Ιστορικό ολοκληρωμένων βεβαιώσεων του ξενοδόχου ──────────────
        [Route("api/HomeApi/GetCertificateHistory")]
        [HttpPost]
        public IHttpActionResult GetCertificateHistory()
        {
            var result = new CertificateHistoryViewModel
            {
                success = false,
                history = new List<CertificateHistoryItemViewModel>()
            };

            try
            {
                HotelDetailsViewModel hotel = GetOwnHotel();
                if (hotel == null)
                    return Ok(result);

                result.success = true;

                // Υπάρχει ενεργός κύκλος;
                result.hasActiveCycle = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.hotelID == hotel.hotelID
                           && x.exploitingCompanyID == hotel.exploitingCompanyID
                           && x.version == 1 && x.isFinished == false)
                    .Any();

                // Ολοκληρωμένοι κύκλοι: όλες οι finished εκδόσεις ομαδοποιημένες ανά certificate
                List<HotelCriteria> finished = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.hotelID == hotel.hotelID
                           && x.exploitingCompanyID == hotel.exploitingCompanyID
                           && x.isFinished == true && x.certificateID.HasValue)
                    .ToList();

                var medals = unitOfWork.MedalRepository.Get().ToList();

                foreach (var group in finished.GroupBy(x => x.certificateID.Value).OrderByDescending(g => g.Key))
                {
                    HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(group.Key);
                    if (cert == null || cert.certificateStatusID != 2) continue;

                    // Αυτο-θεραπεία: εκδομένη αίτηση χωρίς έγγραφο βεβαίωσης → παρ' το τώρα
                    if (!cert.certificateFileID.HasValue)
                    {
                        try { Utils.CertificateDocService.IssueAndStore(unitOfWork, cert.certificateID); }
                        catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs"); }
                    }

                    HotelCriteria v3 = group.FirstOrDefault(x => x.version == 3);
                    string medalTitle = null;
                    if (v3 != null && v3.medalID.HasValue)
                        medalTitle = medals.FirstOrDefault(m => m.id == v3.medalID.Value)?.title;

                    result.history.Add(new CertificateHistoryItemViewModel
                    {
                        certificateID = group.Key,
                        issueDate = cert.issueDateTime.HasValue ? cert.issueDateTime.Value.ToString("dd/MM/yyyy") : "-",
                        validUntil = cert.validityStopDateTime.HasValue ? cert.validityStopDateTime.Value.ToString("dd/MM/yyyy") : "-",
                        isValid = cert.validityStopDateTime.HasValue && cert.validityStopDateTime.Value >= DateTime.Today,
                        totalPoints = v3 != null ? (v3.totalScore ?? v3.totalPoints) : 0,
                        medalTitle = medalTitle,
                        hasV1 = group.Any(x => x.version == 1),
                        hasV2 = group.Any(x => x.version == 2),
                        hasV3 = v3 != null,
                        hasFile = cert.certificateFileID.HasValue
                    });
                }

                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                return Ok(result);
            }
        }

        // ── Dashboard Επιθεωρητή ─────────────────────────────────────────
        [Route("api/HomeApi/GetInspectorDashboard")]
        [HttpPost]
        public IHttpActionResult GetInspectorDashboard()
        {
            var result = new InspectorDashboardViewModel
            {
                success = false,
                upcoming = new List<UpcomingAutopsyViewModel>()
            };

            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                if (user == null || user.role != 10 || !user.tee_inspectorID.HasValue)
                    return Ok(result);

                result.success = true;
                decimal inspID = user.tee_inspectorID.Value;

                // Αναθέσεις του επιθεωρητή (σε εξέλιξη + εκδομένες)
                List<HotelierCertificate> certs = unitOfWork.HotelierCertificateRepository
                    .Get(x => x.tee_inspectorID == inspID && x.certificateTypeID == 84
                           && (x.certificateStatusID == 23 || x.certificateStatusID == 2))
                    .ToList();

                if (certs.Count == 0)
                    return Ok(result);

                List<decimal> certIds = certs.Select(c => c.certificateID).ToList();
                List<HotelCriteria> allCrits = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID.HasValue && certIds.Contains(x.certificateID.Value))
                    .ToList();

                // Τίτλοι/περιοχές από το view
                sql = "Select * from V_TEE_Certificates_Inspector where UserName = @UserName";
                List<CertificateViewModel> viewRows = unitOfWork.context.Database
                    .SqlQuery<CertificateViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .ToList();

                DateTime today = DateTime.Today;

                foreach (var cert in certs)
                {
                    HotelCriteria v2 = allCrits.FirstOrDefault(x => x.certificateID == cert.certificateID && x.version == 2);
                    HotelCriteria v3 = allCrits.FirstOrDefault(x => x.certificateID == cert.certificateID && x.version == 3);
                    int? v2Status = v2 != null ? (int?)v2.status : null;
                    int? v3Status = v3 != null ? (int?)v3.status : null;

                    bool pendingDateConfirm = cert.autopsyDateStatus == 1;  // προτεινόμενη, μη επιβεβαιωμένη
                    bool dateArrived = cert.autopsyDateTime.HasValue && cert.autopsyDateTime.Value.Date <= today;
                    bool canDoAutopsy = v2 != null || dateArrived;
                    bool canDoFinal = v2 != null && v2.status == 2;
                    bool isIssued = cert.certificateStatusID == 2;

                    // Στάδιο (ίδια λογική με τη λίστα Αιτήσεων)
                    if (isIssued) result.countCompleted++;
                    else if (v3Status == 2) result.countAwaitingAcceptance++;
                    else if (v3Status == 1 || canDoFinal) result.countFinal++;
                    else if (v2Status == 1) result.countAutopsyInProgress++;
                    else if (pendingDateConfirm) result.countNew++;   // πρώτα επιβεβαίωση ημ/νίας
                    else if (canDoAutopsy) result.countAutopsyDue++;
                    else if (cert.autopsyDateStatus == 2 || cert.autopsyDateStatus == 3) result.countScheduled++;  // επιβεβαιωμένη, μέλλον
                    else result.countNew++;

                    // Προσεχείς/εκπρόθεσμες αυτοψίες (όχι εκδομένες, αυτοψία όχι υποβεβλημένη)
                    if (!isIssued && (v2 == null || v2.status != 2)
                        && cert.autopsyDateTime.HasValue
                        && cert.autopsyDateTime.Value.Date <= today.AddDays(5))
                    {
                        var row = viewRows.FirstOrDefault(r => r.certificateID == cert.certificateID);
                        int days = (cert.autopsyDateTime.Value.Date - today).Days;
                        result.upcoming.Add(new UpcomingAutopsyViewModel
                        {
                            certificateID = cert.certificateID,
                            hotelTitle = row != null ? row.hotelTitle : ("#" + cert.certificateID),
                            area = row != null ? row.municipalityTitle : "",
                            autopsyDate = cert.autopsyDateTime.Value.ToString("dd/MM/yyyy"),
                            daysUntil = days,
                            overdue = days < 0,
                            isToday = days == 0,
                            pendingConfirm = pendingDateConfirm
                        });
                    }
                }

                result.totalActive = result.countNew + result.countScheduled + result.countAutopsyDue
                                   + result.countAutopsyInProgress + result.countFinal + result.countAwaitingAcceptance;

                result.upcoming = result.upcoming.OrderBy(u => u.daysUntil).Take(8).ToList();

                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                return Ok(result);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Ημερολόγιο: γεγονότα (αυτοψίες + προσωπικές σημειώσεις)
        // ════════════════════════════════════════════════════════════════
        [Route("api/HomeApi/GetCalendarEvents")]
        [HttpPost]
        public IHttpActionResult GetCalendarEvents()
        {
            var result = new CalendarEventsResultViewModel
            {
                success = false,
                events = new List<CalendarEventViewModel>()
            };

            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (user == null) return Ok(result);

                result.success = true;
                DateTime today = DateTime.Today;

                // ── Αυτοψίες (μόνο επιθεωρητής) ──────────────────────────
                if (user.role == 10 && user.tee_inspectorID.HasValue)
                {
                    decimal inspID = user.tee_inspectorID.Value;
                    List<HotelierCertificate> certs = unitOfWork.HotelierCertificateRepository
                        .Get(x => x.tee_inspectorID == inspID && x.certificateTypeID == 84
                               && (x.certificateStatusID == 23 || x.certificateStatusID == 2)
                               && x.autopsyDateTime.HasValue)
                        .ToList();

                    if (certs.Count > 0)
                    {
                        List<decimal> certIds = certs.Select(c => c.certificateID).ToList();
                        List<HotelCriteria> allCrits = unitOfWork.HotelCriteriaRepository
                            .Get(x => x.certificateID.HasValue && certIds.Contains(x.certificateID.Value) && x.version == 2)
                            .ToList();

                        sql = "Select * from V_TEE_Certificates_Inspector where UserName = @UserName";
                        List<CertificateViewModel> viewRows = unitOfWork.context.Database
                            .SqlQuery<CertificateViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                            .ToList();

                        foreach (var cert in certs)
                        {
                            DateTime d = cert.autopsyDateTime.Value.Date;
                            HotelCriteria v2 = allCrits.FirstOrDefault(x => x.certificateID == cert.certificateID);
                            bool done = cert.certificateStatusID == 2 || (v2 != null && v2.status == 2);
                            bool pendingDateConfirm = cert.autopsyDateStatus == 1;

                            string color;
                            string title;
                            string url;
                            if (pendingDateConfirm)
                            {
                                // Προτεινόμενη ημ/νία — πρώτα αποδοχή/αλλαγή, όχι έναρξη αυτοψίας
                                color = "#98a6ad";
                                title = "Επιβεβαίωση ημ/νίας: " + (viewRows.FirstOrDefault(r => r.certificateID == cert.certificateID) != null
                                        ? viewRows.First(r => r.certificateID == cert.certificateID).hotelTitle : ("#" + cert.certificateID));
                                url = "/Certificate?certId=" + cert.certificateID;
                            }
                            else
                            {
                                var r0 = viewRows.FirstOrDefault(r => r.certificateID == cert.certificateID);
                                title = "Αυτοψία: " + (r0 != null ? r0.hotelTitle : ("#" + cert.certificateID));

                                if (done)
                                {
                                    color = "#1D9E75";                        // ολοκληρωμένη
                                    url = "/Certificate/ViewCertificate/" + cert.certificateID + "?mode=2";
                                }
                                else if (d <= today)
                                {
                                    color = d < today ? "#fa5c7c" : "#ffbc00"; // εκπρόθεσμη / σήμερα → έναρξη αυτοψίας
                                    url = "/Certificate/ViewCertificate/" + cert.certificateID + "?mode=2";
                                }
                                else
                                {
                                    color = "#39afd1";                         // προγραμματισμένη μελλοντική → μόνο προβολή
                                    url = "/Certificate?certId=" + cert.certificateID;
                                }
                            }

                            result.events.Add(new CalendarEventViewModel
                            {
                                id = "autopsy-" + cert.certificateID,
                                title = title,
                                start = d.ToString("yyyy-MM-dd"),
                                color = color,
                                type = "autopsy",
                                editable = false,
                                isDone = done,
                                url = url
                            });
                        }
                    }
                }

                // ── Προσωπικές σημειώσεις (όλοι οι ρόλοι) ─────────────────
                string uname = User.Identity.Name;
                List<UserCalendarNote> notes = unitOfWork.UserCalendarNoteRepository
                    .Get(x => x.userName == uname)
                    .ToList();

                foreach (var n in notes)
                {
                    result.events.Add(new CalendarEventViewModel
                    {
                        id = "note-" + n.noteID,
                        title = n.title,
                        start = n.noteDate.ToString("yyyy-MM-dd"),
                        color = string.IsNullOrEmpty(n.color) ? "#727cf5" : n.color,
                        type = "note",
                        editable = true,
                        isDone = n.isDone,
                        url = null
                    });
                }

                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                return Ok(result);
            }
        }

        // Αποθήκευση/ενημέρωση προσωπικής σημείωσης
        [Route("api/HomeApi/SaveCalendarNote")]
        [HttpPost]
        public IHttpActionResult SaveCalendarNote([FromBody] CalendarNoteRequest req)
        {
            var result = new CalendarNoteResultViewModel { success = false };
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.title) || string.IsNullOrWhiteSpace(req.noteDate))
                {
                    result.message = "Συμπληρώστε τίτλο και ημερομηνία.";
                    return Ok(result);
                }

                DateTime date;
                if (!DateTime.TryParse(req.noteDate, out date))
                {
                    result.message = "Μη έγκυρη ημερομηνία.";
                    return Ok(result);
                }

                string uname = User.Identity.Name;
                UserCalendarNote note;

                if (req.noteID.HasValue && req.noteID.Value > 0)
                {
                    note = unitOfWork.UserCalendarNoteRepository.GetByID(req.noteID.Value);
                    if (note == null || note.userName != uname)   // ιδιοκτησία
                    {
                        result.message = "Η σημείωση δεν βρέθηκε.";
                        return Ok(result);
                    }
                    note.title = req.title.Trim();
                    note.noteDate = date;
                    note.color = req.color;
                    note.isDone = req.isDone;
                    unitOfWork.UserCalendarNoteRepository.Update(note);
                }
                else
                {
                    note = new UserCalendarNote
                    {
                        userName = uname,
                        title = req.title.Trim(),
                        noteDate = date,
                        color = string.IsNullOrEmpty(req.color) ? "#727cf5" : req.color,
                        isDone = false,
                        creationDateTime = DateTime.Now
                    };
                    unitOfWork.UserCalendarNoteRepository.Insert(note);
                }

                unitOfWork.Save();
                result.success = true;
                result.noteID = note.noteID;
                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                result.message = "Σφάλμα αποθήκευσης.";
                return Ok(result);
            }
        }

        // Διαγραφή σημείωσης
        [Route("api/HomeApi/DeleteCalendarNote")]
        [HttpPost]
        public IHttpActionResult DeleteCalendarNote([FromBody] CalendarNoteRequest req)
        {
            var result = new CalendarNoteResultViewModel { success = false };
            try
            {
                if (req == null || !req.noteID.HasValue) return Ok(result);
                string uname = User.Identity.Name;
                UserCalendarNote note = unitOfWork.UserCalendarNoteRepository.GetByID(req.noteID.Value);
                if (note == null || note.userName != uname) return Ok(result);

                unitOfWork.UserCalendarNoteRepository.Delete(note);
                unitOfWork.Save();
                result.success = true;
                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                return Ok(result);
            }
        }

        // ── Πλήθος εκδομένων βεβαιώσεων ανά μετάλλιο (admin dashboard) ────
        [Route("api/HomeApi/GetMedalCounts")]
        [HttpPost]
        public IHttpActionResult GetMedalCounts()
        {
            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (user == null || user.role != 100)
                    return Ok(new { success = false });

                List<MedalViewModel> medals = unitOfWork.context.Database
                    .SqlQuery<MedalViewModel>("Select * from V_TEE_Medals order by min").ToList();

                // Εκδομένες βεβαιώσεις (status=2) — το μετάλλιο από την τελική κατάταξη (v3)
                List<HotelierCertificate> issued = unitOfWork.HotelierCertificateRepository
                    .Get(x => x.certificateTypeID == 84 && x.certificateStatusID == 2).ToList();
                List<decimal> certIds = issued.Select(c => c.certificateID).ToList();

                Dictionary<decimal, decimal?> medalByCert = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.version == 3 && x.certificateID.HasValue && certIds.Contains(x.certificateID.Value))
                    .GroupBy(x => x.certificateID.Value)
                    .ToDictionary(g => g.Key, g => g.First().medalID);

                // Αν δεν υπάρχει μετάλλιο → Αταξινόμητο (id=5)
                const decimal unclassifiedId = 5;
                System.Func<HotelierCertificate, decimal> medalOf = c =>
                {
                    decimal? mid;
                    return (medalByCert.TryGetValue(c.certificateID, out mid) && mid.HasValue) ? mid.Value : unclassifiedId;
                };

                // (α) ανά ΒΕΒΑΙΩΣΗ: όλες οι εκδομένες
                var certTally = new Dictionary<decimal, int>();
                foreach (var c in issued)
                {
                    decimal use = medalOf(c);
                    certTally[use] = (certTally.ContainsKey(use) ? certTally[use] : 0) + 1;
                }

                // (β) ανά ΚΑΤΑΛΥΜΑ (distinct): το μετάλλιο της ΠΙΟ ΠΡΟΣΦΑΤΗΣ βεβαίωσης κάθε καταλύματος
                List<HotelierCertificate> latestPerHotel = issued
                    .GroupBy(c => (c.hotelID ?? "") + "|" + (c.exploitingCompanyID ?? ""))
                    .Select(g => g.OrderByDescending(c => c.issueDateTime ?? DateTime.MinValue)
                                  .ThenByDescending(c => c.certificateID).First())
                    .ToList();

                var hotelTally = new Dictionary<decimal, int>();
                foreach (var c in latestPerHotel)
                {
                    decimal use = medalOf(c);
                    hotelTally[use] = (hotelTally.ContainsKey(use) ? hotelTally[use] : 0) + 1;
                }

                var result = medals.Select(m => new
                {
                    medalID = m.id,
                    title = m.title,
                    certCount = certTally.ContainsKey(m.id) ? certTally[m.id] : 0,
                    hotelCount = hotelTally.ContainsKey(m.id) ? hotelTally[m.id] : 0
                }).ToList();

                return Ok(new
                {
                    success = true,
                    totalCertificates = issued.Count,
                    totalHotels = latestPerHotel.Count,
                    medals = result
                });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "HomeApiController.cs");
                return Ok(new { success = false });
            }
        }

    }
}
