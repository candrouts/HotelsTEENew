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
    public class InspectorSelectionApiController : ApiController
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        [Route("api/InspectorSelectionApi/GetInitialData")]
        [HttpPost]
        public IHttpActionResult GetInitialData()
        {
            try
            {
                string sql = "SELECT * FROM V_TEE_HotelDetails WHERE UserName = @UserName";
                HotelDetailsViewModel hotelDetails = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                if (hotelDetails == null)
                    return Ok(new InspectorInitialDataViewModel
                    {
                        hotelCriteriaStatus = 0,
                        timeline = BuildTimeline(null, null, null)
                    });

                sql = "SELECT * FROM V_TEE_HotelCriteria WHERE hotelID = @hotelID AND exploitingCompanyID = @companyID AND version=1 AND isFinished=0";
                HotelCriteriaViewModel hotelCriteria = unitOfWork.context.Database
                    .SqlQuery<HotelCriteriaViewModel>(sql,
                        new SqlParameter("@hotelID", hotelDetails.hotelID),
                        new SqlParameter("@companyID", hotelDetails.exploitingCompanyID))
                    .FirstOrDefault();

                sql = "SELECT kalID, title, levelID, parentID FROM ELSTATAreas WHERE levelID=3 AND isActive=1 ORDER BY title";
                List<ELSTATAreaViewModel> perifereies = unitOfWork.context.Database.SqlQuery<ELSTATAreaViewModel>(sql).ToList();

                var result = new InspectorInitialDataViewModel
                {
                    perifereies = perifereies,
                    hotelCriteriaID = hotelCriteria?.id,
                    hotelCriteriaStatus = hotelCriteria?.status ?? 0
                };

                // Ελέγχουμε αν το HotelCriteria έχει ήδη συνδεδεμένο HotelierCertificate
                HotelierCertificate certificate = null;
                if (hotelCriteria != null && hotelCriteria.certificateID.HasValue)
                {
                    certificate = unitOfWork.HotelierCertificateRepository
                        .GetByID(hotelCriteria.certificateID.Value);

                    // Απορρίφθηκε από επιθεωρητή (status=24): δεν είναι ενεργή αίτηση —
                    // ο ξενοδόχος ξαναεπιλέγει επιθεωρητή. Δείχνουμε alert με το ιστορικό απόρριψης.
                    if (certificate != null && certificate.certificateStatusID == 24)
                    {
                        Inspector rejInsp = certificate.tee_inspectorID.HasValue
                            ? unitOfWork.InspectorRepository.GetByID(certificate.tee_inspectorID.Value)
                            : null;
                        result.assignmentRejected = true;
                        result.rejectedInspectorName = rejInsp != null
                            ? rejInsp.firstName + " " + rejInsp.lastName : "-";
                        result.rejectionNote = certificate.notes;

                        certificate = null;   // η φόρμα αναζήτησης ξαναεμφανίζεται
                    }

                    if (certificate != null)
                    {
                        Inspector insp = certificate.tee_inspectorID.HasValue
                            ? unitOfWork.InspectorRepository.GetByID(certificate.tee_inspectorID.Value)
                            : null;

                        // Όλες οι εκδόσεις του κύκλου (v1/v2/v3) για στάδια & τελική βαθμολογία
                        decimal certID = certificate.certificateID;
                        List<HotelCriteria> cycleVersions = unitOfWork.HotelCriteriaRepository
                            .Get(x => x.certificateID == certID).ToList();

                        HotelCriteria cv1 = cycleVersions.FirstOrDefault(x => x.version == 1);
                        HotelCriteria cv2 = cycleVersions.FirstOrDefault(x => x.version == 2);
                        HotelCriteria cv3 = cycleVersions.FirstOrDefault(x => x.version == 3);

                        bool isIssued = certificate.certificateStatusID == 2;
                        bool awaiting = !isIssued && cv3 != null && cv3.status == 2;

                        string medalTitle = null;
                        if (cv3 != null && cv3.medalID.HasValue)
                        {
                            Medal medal = unitOfWork.MedalRepository.GetByID(cv3.medalID.Value);
                            medalTitle = medal?.title;
                        }

                        result.existingCertificate = new ExistingCertificateInfoViewModel
                        {
                            inspectorName = insp != null ? insp.firstName + " " + insp.lastName : "-",
                            inspectorEmail = insp?.email,
                            inspectorPhone = insp?.phone,
                            proposedDate = certificate.autopsyDateTime.HasValue ? certificate.autopsyDateTime.Value.ToString("dd/MM/yyyy") : "-",
                            submissionDate = certificate.creationDateTime.ToString("dd/MM/yyyy"),
                            certificateID = certificate.certificateID,
                            autopsyDateStatus = certificate.autopsyDateStatus,
                            v1Status = cv1 != null ? (int?)cv1.status : null,
                            v2Status = cv2 != null ? (int?)cv2.status : null,
                            v3Status = cv3 != null ? (int?)cv3.status : null,
                            awaitingAcceptance = awaiting,
                            isIssued = isIssued,
                            finalTotalPoints = cv3 != null && cv3.status == 2 ? (cv3.totalScore ?? cv3.totalPoints) : (decimal?)null,
                            finalMedalTitle = medalTitle
                        };
                    }
                }

                // Χτίζουμε το Timeline
                result.timeline = BuildTimeline(hotelDetails, hotelCriteria, certificate);

                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs");
                return Ok(new InspectorInitialDataViewModel
                {
                    hotelCriteriaStatus = 0,
                    timeline = BuildTimeline(null, null, null)
                });
            }
        }

        private List<TimelineStepViewModel> BuildTimeline(
            HotelDetailsViewModel hotelDetails,
            HotelCriteriaViewModel hotelCriteria,
            HotelierCertificate certificate)
        {
            var steps = new List<TimelineStepViewModel>();

            // ── Βήμα 1: Αυτοαξιολόγηση (version=1) ──────────────────────────────
            bool step1Done    = hotelCriteria != null && hotelCriteria.status == 2;
            bool step1Started = hotelCriteria != null && hotelCriteria.creationDatetime.HasValue;

            string step1Date     = null;
            string step1Subtitle = "Αναμένεται η έναρξη αυτοαξιολόγησης";

            if (step1Done && hotelCriteria.lastModificationDateTime.HasValue)
            {
                step1Date     = hotelCriteria.lastModificationDateTime.Value.ToString("dd/MM/yyyy");
                step1Subtitle = "Οριστική υποβολή κριτηρίων αυτοαξιολόγησης";
            }
            else if (step1Started)
            {
                step1Date     = hotelCriteria.creationDatetime.Value.ToString("dd/MM/yyyy");
                step1Subtitle = "Αναμένεται η υποβολή της αυτοαξιολόγησης";
            }

            steps.Add(new TimelineStepViewModel
            {
                title       = "Αυτοαξιολόγηση",
                subtitle    = step1Subtitle,
                date        = step1Date,
                isCompleted = step1Done,
                isPending   = step1Started && !step1Done,
                pendingLabel = "Σε εξέλιξη",
                icon        = "mdi mdi-clipboard-check-outline",
                colorClass  = step1Done ? "success" : (step1Started ? "primary" : "secondary")
            });

            // ── Βήμα 2: Υποβολή Αίτησης Αξιολόγησης ──────────────────────────
            bool step2Done = certificate != null;
            steps.Add(new TimelineStepViewModel
            {
                title      = "Υποβολή Αίτησης",
                subtitle   = certificate != null
                                ? (certificate.tee_inspectorID.HasValue
                                    ? "Ο επιθεωρητής έχει ενημερωθεί"
                                    : "")
                                : "Επιλογή επιθεωρητή και προτεινόμενης ημερομηνίας",
                date       = certificate != null ? certificate.creationDateTime.ToString("dd/MM/yyyy") : null,
                isCompleted = step2Done,
                isPending  = false,
                icon       = "mdi mdi-send-outline",
                colorClass = step2Done ? "success" : "secondary"
            });

            // ── Βήμα 3: Ημερομηνία Αυτοψίας ──────────────────────────────────
            // autopsyDateStatus: 1=πρόταση ξενοδόχου (εκκρεμεί), 2=αποδοχή, 3=οριστική με αλλαγή
            bool step3HasDate   = certificate != null && certificate.autopsyDateTime.HasValue;
            bool step3Confirmed = step3HasDate &&
                                  (certificate.autopsyDateStatus == 2 || certificate.autopsyDateStatus == 3);

            string step3Subtitle = "Αναμένεται ο ορισμός ημερομηνίας";
            if (step3Confirmed)
                step3Subtitle = certificate.autopsyDateStatus == 2
                    ? "Η προτεινόμενη ημερομηνία έγινε αποδεκτή από τον επιθεωρητή"
                    : "Οριστική ημερομηνία (ορίστηκε από τον επιθεωρητή)";
            else if (step3HasDate)
                step3Subtitle = "Προτεινόμενη ημερομηνία — αναμένεται επιβεβαίωση από τον επιθεωρητή";

            steps.Add(new TimelineStepViewModel
            {
                title      = "Αυτοψία Επιθεωρητή",
                subtitle   = step3Subtitle,
                date       = step3HasDate ? certificate.autopsyDateTime.Value.ToString("dd/MM/yyyy") : null,
                isCompleted = step3Confirmed,
                isPending  = step3HasDate && !step3Confirmed,
                pendingLabel = "Προγραμματισμένη",
                icon       = "mdi mdi-calendar-clock",
                colorClass = step3Confirmed ? "success" : (step3HasDate ? "primary" : "secondary")
            });

            // ── Βήμα 4: Αξιολόγηση Επιθεωρητή (version=2) ─────────────────────
            // status=1 → σε εξέλιξη (creationDatetime), status=2 → ολοκληρώθηκε (lastModificationDateTime)
            HotelCriteriaViewModel inspectorCriteria = null;
            if (hotelDetails != null && certificate != null)
            {
                string sql4 = "SELECT * FROM V_TEE_HotelCriteria WHERE hotelID = @hotelID AND exploitingCompanyID = @companyID AND version=2 AND certificateID = @certificateID";
                inspectorCriteria = unitOfWork.context.Database
                    .SqlQuery<HotelCriteriaViewModel>(sql4,
                        new SqlParameter("@hotelID", hotelDetails.hotelID),
                        new SqlParameter("@companyID", hotelDetails.exploitingCompanyID),
                        new SqlParameter("@certificateID", certificate.certificateID))
                    .FirstOrDefault();
            }
            bool step4Done    = inspectorCriteria != null && inspectorCriteria.status == 2;
            bool step4Started = inspectorCriteria != null && !step4Done;

            string step4Date = null;
            string step4Subtitle = "Αναμένεται η αξιολόγηση από τον επιθεωρητή";
            if (step4Done)
            {
                step4Date = inspectorCriteria.lastModificationDateTime.HasValue
                                ? inspectorCriteria.lastModificationDateTime.Value.ToString("dd/MM/yyyy") : null;
                step4Subtitle = "Ο επιθεωρητής υπέβαλε τα αποτελέσματα";
            }
            else if (step4Started)
            {
                step4Date = inspectorCriteria.creationDatetime.HasValue
                                ? inspectorCriteria.creationDatetime.Value.ToString("dd/MM/yyyy") : null;
                step4Subtitle = "Η αξιολόγηση από τον επιθεωρητή είναι σε εξέλιξη";
            }

            steps.Add(new TimelineStepViewModel
            {
                title      = "Αξιολόγηση Επιθεωρητή",
                subtitle   = step4Subtitle,
                date       = step4Date,
                isCompleted = step4Done,
                isPending  = step4Started,
                pendingLabel = "Σε εξέλιξη",
                icon       = "mdi mdi-account-check-outline",
                colorClass = step4Done ? "success" : (step4Started ? "primary" : "secondary")
            });

            // ── Βήμα 5: Τελική Κατάταξη (version=3) ──────────────────────────
            HotelCriteriaViewModel finalCriteria = null;
            if (hotelDetails != null && certificate != null)
            {
                string sql5 = "SELECT * FROM V_TEE_HotelCriteria WHERE hotelID = @hotelID AND exploitingCompanyID = @companyID AND version=3 AND certificateID = @certificateID";
                finalCriteria = unitOfWork.context.Database
                    .SqlQuery<HotelCriteriaViewModel>(sql5,
                        new SqlParameter("@hotelID", hotelDetails.hotelID),
                        new SqlParameter("@companyID", hotelDetails.exploitingCompanyID),
                        new SqlParameter("@certificateID", certificate.certificateID))
                    .FirstOrDefault();
            }
            bool step5Done    = finalCriteria != null && finalCriteria.status == 2;
            bool step5Started = finalCriteria != null && !step5Done;

            string step5Date = null;
            string step5Subtitle = "Αναμένεται η τελική κατάταξη";
            if (step5Done)
            {
                step5Date = finalCriteria.lastModificationDateTime.HasValue
                                ? finalCriteria.lastModificationDateTime.Value.ToString("dd/MM/yyyy") : null;
                step5Subtitle = "Η τελική κατάταξη ολοκληρώθηκε — το πιστοποιητικό εκδόθηκε";
            }
            else if (step5Started)
            {
                step5Date = finalCriteria.creationDatetime.HasValue
                                ? finalCriteria.creationDatetime.Value.ToString("dd/MM/yyyy") : null;
                step5Subtitle = "Η τελική κατάταξη είναι σε εξέλιξη";
            }

            steps.Add(new TimelineStepViewModel
            {
                title      = "Τελική Κατάταξη",
                subtitle   = step5Subtitle,
                date       = step5Date,
                isCompleted = step5Done,
                isPending  = step5Started,
                pendingLabel = "Σε εξέλιξη",
                icon       = "mdi mdi-certificate-outline",
                colorClass = step5Done ? "success" : (step5Started ? "primary" : "secondary")
            });

            // ── Βήμα 6: Αποδοχή & Έκδοση Βεβαίωσης ──────────────────────────
            bool step6Done    = certificate != null && certificate.certificateStatusID == 2;
            bool step6Pending = !step6Done && step5Done;   // v3 υποβλήθηκε, αναμένεται αποδοχή ξενοδόχου

            steps.Add(new TimelineStepViewModel
            {
                title      = "Έκδοση Βεβαίωσης",
                subtitle   = step6Done
                                ? "Η βεβαίωση εκδόθηκε — ισχύς έως " +
                                  (certificate.validityStopDateTime.HasValue ? certificate.validityStopDateTime.Value.ToString("dd/MM/yyyy") : "-")
                                : (step6Pending
                                    ? "Αναμένεται η αποδοχή της τελικής κατάταξης από εσάς"
                                    : "Αναμένεται η ολοκλήρωση των προηγούμενων σταδίων"),
                date       = step6Done && certificate.issueDateTime.HasValue
                                ? certificate.issueDateTime.Value.ToString("dd/MM/yyyy") : null,
                isCompleted = step6Done,
                isPending  = step6Pending,
                pendingLabel = "Αναμονή αποδοχής",
                icon       = "mdi mdi-seal",
                colorClass = step6Done ? "success" : (step6Pending ? "primary" : "secondary")
            });

            return steps;
        }

        [Route("api/InspectorSelectionApi/GetPerifereiakesEnotites")]
        [HttpPost]
        public IHttpActionResult GetPerifereiakesEnotites([FromBody] ELSTATAreaViewModel filter)
        {
            try
            {
                string sql = "SELECT kalID, title, levelID, parentID FROM ELSTATAreas WHERE levelID=4 AND isActive=1 AND parentID=@parentID ORDER BY title";
                List<ELSTATAreaViewModel> pes = unitOfWork.context.Database
                    .SqlQuery<ELSTATAreaViewModel>(sql, new SqlParameter("@parentID", filter.kalID ?? ""))
                    .ToList();
                return Ok(pes);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs");
                return Ok(new List<ELSTATAreaViewModel>());
            }
        }

        [Route("api/InspectorSelectionApi/GetInspectors")]
        [HttpPost]
        public IHttpActionResult GetInspectors([FromBody] InspectorFilterViewModel filter)
        {
            try
            {
                /*
                 * Λογική φίλτρου περιοχής:
                 *
                 * Αν επιλέχθηκε ΠΕ (peKalID):
                 *   (α) inspectors με row kalID = αυτή η ΠΕ  (levelID=4)
                 *   (β) inspectors με row kalID = Περιφέρεια της ΠΕ (levelID=3)
                 *
                 * Αν επιλέχθηκε μόνο Περιφέρεια (perifereaKalID):
                 *   (α) inspectors με row kalID = η Περιφέρεια (levelID=3)
                 *   (β) inspectors με row kalID = οποιαδήποτε ΠΕ της Περιφέρειας (levelID=4)
                 */
                string areaWhere = "";
                List<SqlParameter> parameters = new List<SqlParameter>();

                if (!string.IsNullOrEmpty(filter?.peKalID))
                {
                    areaWhere = @"
                        AND i.id IN (
                            SELECT ia.inspectorID FROM TEE_Inspector_Areas ia
                            WHERE ia.kalID = @peKalID
                            UNION
                            SELECT ia2.inspectorID FROM TEE_Inspector_Areas ia2
                            INNER JOIN ELSTATAreas ea ON ea.kalID = @peKalID
                            WHERE ia2.kalID = ea.parentID AND ia2.levelID = 3
                        )";
                    parameters.Add(new SqlParameter("@peKalID", filter.peKalID));
                }
                else if (!string.IsNullOrEmpty(filter?.perifereaKalID))
                {
                    areaWhere = @"
                        AND i.id IN (
                            SELECT ia.inspectorID FROM TEE_Inspector_Areas ia
                            WHERE ia.kalID = @perKalID AND ia.levelID = 3
                            UNION
                            SELECT ia2.inspectorID FROM TEE_Inspector_Areas ia2
                            INNER JOIN ELSTATAreas ea ON ea.kalID = ia2.kalID
                            WHERE ea.parentID = @perKalID AND ia2.levelID = 4
                        )";
                    parameters.Add(new SqlParameter("@perKalID", filter.perifereaKalID));
                }

                string nameWhere = "";
                if (!string.IsNullOrEmpty(filter?.name))
                {
                    nameWhere = " AND (i.firstName LIKE @name OR i.lastName LIKE @name)";
                    parameters.Add(new SqlParameter("@name", "%" + filter.name + "%"));
                }

                string sql = @"
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
                    WHERE i.isActive = 1
                    " + areaWhere + nameWhere + @"
                    ORDER BY i.lastName, i.firstName";

                List<InspectorSearchViewModel> inspectors = unitOfWork.context.Database
                    .SqlQuery<InspectorSearchViewModel>(sql, parameters.Cast<object>().ToArray())
                    .ToList();
                return Ok(inspectors);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs");
                return Ok(new List<InspectorSearchViewModel>());
            }
        }

        [Route("api/InspectorSelectionApi/SubmitRequest")]
        [HttpPost]
        public IHttpActionResult SubmitRequest([FromBody] InspectorSubmitViewModel model)
        {
            try
            {
                if (model == null || model.hotelCriteriaID == 0 || model.inspectorID == 0)
                    return Ok(new ApiAnswer { success = false });

                HotelCriteria hotelCriteria = unitOfWork.HotelCriteriaRepository.GetByID(model.hotelCriteriaID);
                if (hotelCriteria == null || hotelCriteria.status != 2)
                    return Ok(new ApiAnswer { success = false });

                DateTime? proposed = null;
                if (!string.IsNullOrEmpty(model.proposedDate))
                {
                    DateTime parsed;
                    if (DateTime.TryParseExact(model.proposedDate, new[] { "dd/MM/yyyy", "yyyy-MM-dd" },
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out parsed))
                        proposed = parsed;
                }

                Inspector inspector = unitOfWork.InspectorRepository.GetByID(model.inspectorID);
                if (inspector == null)
                    return Ok(new ApiAnswer { success = false });

                // Reuse: αν υπάρχει ήδη certificate που απορρίφθηκε από επιθεωρητή (status=24),
                // επαναχρησιμοποιείται με νέο επιθεωρητή/ημερομηνία (διατηρώντας το ιστορικό notes).
                HotelierCertificate certificate = null;
                if (hotelCriteria.certificateID.HasValue)
                {
                    HotelierCertificate existing = unitOfWork.HotelierCertificateRepository
                        .GetByID(hotelCriteria.certificateID.Value);
                    if (existing != null && existing.certificateStatusID == 24)
                        certificate = existing;
                }

                if (certificate != null)
                {
                    certificate.certificateStatusID = 23;
                    certificate.tee_inspectorID = inspector.id;
                    certificate.autopsyDateTime = proposed;
                    certificate.autopsyDateStatus = proposed.HasValue ? (int?)1 : null;
                    certificate.autopsyDateConfirmationDateTime = null;
                    unitOfWork.HotelierCertificateRepository.Update(certificate);
                }
                else
                {
                    certificate = new HotelierCertificate
                    {
                        hotelID = hotelCriteria.hotelID,
                        exploitingCompanyID = hotelCriteria.exploitingCompanyID,
                        certificateStatusID = 23,
                        certificateTypeID = 84,
                        tee_inspectorID = inspector.id,
                        autopsyDateTime = proposed,
                        autopsyDateStatus = proposed.HasValue ? (int?)1 : null,  // 1 = εκκρεμεί απάντηση επιθεωρητή
                        creationDateTime = DateTime.Now,
                        responsibleUserID = 3
                    };
                    unitOfWork.HotelierCertificateRepository.Insert(certificate);
                    hotelCriteria.certificate = certificate;
                    unitOfWork.HotelCriteriaRepository.Update(hotelCriteria);
                }

                unitOfWork.Save();

                // Ειδοποίηση: νέα ανάθεση → επιθεωρητής
                Utils.NotificationService.Fire("ASSIGNMENT_SUBMITTED", certificate.certificateID,
                    new Dictionary<string, string> {
                        { "hotelName", Utils.NotificationService.HotelName(certificate.certificateID) },
                        { "inspectorName", inspector.firstName + " " + inspector.lastName },
                        { "proposedDate", proposed.HasValue ? proposed.Value.ToString("dd/MM/yyyy") : "-" },
                        { "link", Utils.NotificationService.Link("/Certificate") }
                    });

                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs");
                return Ok(new ApiAnswer { success = false });
            }
        }

        // Επιστρέφει το certificate του ΕΝΕΡΓΟΥ κύκλου του logged-in ξενοδόχου (έλεγχος ιδιοκτησίας)
        private HotelierCertificate GetOwnActiveCertificate()
        {
            string sql = "SELECT * FROM V_TEE_HotelDetails WHERE UserName = @UserName";
            HotelDetailsViewModel hotelDetails = unitOfWork.context.Database
                .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
            if (hotelDetails == null) return null;

            HotelCriteria v1 = unitOfWork.HotelCriteriaRepository
                .Get(x => x.hotelID == hotelDetails.hotelID
                       && x.exploitingCompanyID == hotelDetails.exploitingCompanyID
                       && x.version == 1 && x.isFinished == false)
                .FirstOrDefault();

            if (v1 == null || !v1.certificateID.HasValue) return null;

            return unitOfWork.HotelierCertificateRepository.GetByID(v1.certificateID.Value);
        }

        // ── Αποδοχή τελικής κατάταξης από ξενοδόχο = Έκδοση Βεβαίωσης ──────
        // certificateStatusID=2, issueDateTime=now, ισχύς 3 έτη, isFinished=1 σε όλες τις εκδόσεις
        [Route("api/InspectorSelectionApi/AcceptFinal")]
        [HttpPost]
        public IHttpActionResult AcceptFinal()
        {
            try
            {
                HotelierCertificate certificate = GetOwnActiveCertificate();
                if (certificate == null)
                    return Ok(new ApiAnswer { success = false });

                decimal certID = certificate.certificateID;

                // Πρέπει να υπάρχει οριστικά υποβεβλημένη τελική κατάταξη (v3, status=2)
                HotelCriteria v3 = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == certID && x.version == 3 && x.status == 2)
                    .FirstOrDefault();
                if (v3 == null || certificate.certificateStatusID == 2)
                    return Ok(new ApiAnswer { success = false });

                certificate.certificateStatusID = 2;
                certificate.issueDateTime = DateTime.Now;
                certificate.validityMonths = 36;
                certificate.validityStartDateTime = DateTime.Now;
                certificate.validityStopDateTime = DateTime.Now.AddYears(3);
                unitOfWork.HotelierCertificateRepository.Update(certificate);

                // Κλείσιμο κύκλου: isFinished=1 σε όλες τις εκδόσεις του certificate
                List<HotelCriteria> versions = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == certID).ToList();
                foreach (var v in versions)
                {
                    v.isFinished = true;
                    unitOfWork.HotelCriteriaRepository.Update(v);
                }

                unitOfWork.Save();

                // Αυτόματη έκδοση & αποθήκευση της βεβαίωσης (PDF/HTML) με σύνδεση
                // certificateFileID + ανάρτηση στη Διαύγεια (η Διαύγεια μόνο όταν enabled).
                try { Utils.CertificateDocService.IssueAndStore(unitOfWork, certID); }
                catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs"); /* η αποτυχία εγγράφου δεν ακυρώνει την αποδοχή */ }

                // Ειδοποίηση: εκδόθηκε βεβαίωση → ξενοδόχος
                Utils.NotificationService.Fire("CERTIFICATE_ISSUED", certID,
                    new Dictionary<string, string> {
                        { "hotelName", Utils.NotificationService.HotelName(certID) },
                        { "validUntil", certificate.validityStopDateTime.HasValue ? certificate.validityStopDateTime.Value.ToString("dd/MM/yyyy") : "-" },
                        { "link", Utils.NotificationService.Link("/Home") }
                    });

                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs");
                return Ok(new ApiAnswer { success = false });
            }
        }

        // ── Απόρριψη τελικής κατάταξης από ξενοδόχο ─────────────────────────
        // Η v3 επιστρέφει σε draft (status=1) για διόρθωση από τον επιθεωρητή.
        // Το σχόλιο αποθηκεύεται στο notes του certificate.
        [Route("api/InspectorSelectionApi/RejectFinal")]
        [HttpPost]
        public IHttpActionResult RejectFinal([FromBody] RejectFinalViewModel model)
        {
            try
            {
                HotelierCertificate certificate = GetOwnActiveCertificate();
                if (certificate == null || certificate.certificateStatusID == 2)
                    return Ok(new ApiAnswer { success = false });

                decimal certID = certificate.certificateID;

                HotelCriteria v3 = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == certID && x.version == 3 && x.status == 2)
                    .FirstOrDefault();
                if (v3 == null)
                    return Ok(new ApiAnswer { success = false });

                // Πίσω σε draft για διόρθωση από τον επιθεωρητή
                v3.status = 1;
                unitOfWork.HotelCriteriaRepository.Update(v3);

                // Σχόλιο απόρριψης στο notes του certificate (σταθερός marker)
                string comment = (model?.comment ?? "").Trim();
                string entry = Utils.CertificateNotes.FormatFinalRejection(comment);
                certificate.notes = string.IsNullOrEmpty(certificate.notes)
                    ? entry
                    : certificate.notes + Environment.NewLine + entry;
                unitOfWork.HotelierCertificateRepository.Update(certificate);

                unitOfWork.Save();

                // Ειδοποίηση: απόρριψη τελικής κατάταξης → επιθεωρητής
                Utils.NotificationService.Fire("FINAL_REJECTED", certID,
                    new Dictionary<string, string> {
                        { "hotelName", Utils.NotificationService.HotelName(certID) },
                        { "comment", comment },
                        { "link", Utils.NotificationService.Link("/Certificate") }
                    });

                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "InspectorSelectionApiController.cs");
                return Ok(new ApiAnswer { success = false });
            }
        }
    }
}
