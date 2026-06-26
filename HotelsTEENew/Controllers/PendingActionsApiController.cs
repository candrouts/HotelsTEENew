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
    // Εκκρεμείς ενέργειες χρήστη — υπολογίζονται live από το state της βάσης
    // (δεν υπάρχει πίνακας notifications).
    [Authorize]
    public class PendingActionsApiController : ApiController
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        [Route("api/PendingActionsApi/GetPendingActions")]
        [HttpPost]
        public IHttpActionResult GetPendingActions()
        {
            var result = new PendingActionsResultViewModel
            {
                success = false,
                actions = new List<PendingActionViewModel>()
            };

            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                if (user == null)
                    return Ok(result);

                result.success = true;

                if (user.role == 1)
                    result.actions = GetHotelierActions();
                else if (user.role == 10 && user.tee_inspectorID.HasValue)
                    result.actions = GetInspectorActions(user.tee_inspectorID.Value);

                // Προσωπικές σημειώσεις ημερολογίου για σήμερα/αύριο (όλοι οι ρόλοι)
                result.actions.AddRange(GetCalendarNoteActions());

                return Ok(result);
            }
            catch (Exception)
            {
                return Ok(result);
            }
        }

        // ── Εκκρεμότητες Ξενοδόχου ──────────────────────────────────────
        private List<PendingActionViewModel> GetHotelierActions()
        {
            var actions = new List<PendingActionViewModel>();

            string sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
            HotelDetailsViewModel hotel = unitOfWork.context.Database
                .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
            if (hotel == null) return actions;

            // Ενεργός κύκλος
            HotelCriteria v1 = unitOfWork.HotelCriteriaRepository
                .Get(x => x.hotelID == hotel.hotelID && x.exploitingCompanyID == hotel.exploitingCompanyID
                       && x.version == 1 && x.isFinished == false)
                .FirstOrDefault();

            if (v1 == null)
                return actions;   // δεν υπάρχει ενεργή διαδικασία → καμία εκκρεμότητα

            if (v1.status == 1)
            {
                actions.Add(new PendingActionViewModel
                {
                    title = "Ολοκληρώστε την αυτοαξιολόγησή σας",
                    description = "Η αυτοαξιολόγηση είναι σε εξέλιξη — συμπληρώστε τα κριτήρια και υποβάλετέ την οριστικά.",
                    link = "/Criteria",
                    icon = "mdi mdi-clipboard-edit-outline",
                    colorClass = "warning"
                });
                return actions;
            }

            // v1 υποβλήθηκε
            if (!v1.certificateID.HasValue)
            {
                actions.Add(new PendingActionViewModel
                {
                    title = "Υποβάλετε αίτηση αξιολόγησης",
                    description = "Η αυτοαξιολόγηση υποβλήθηκε — επιλέξτε επιθεωρητή και προτεινόμενη ημερομηνία επιθεώρησης.",
                    link = "/InspectorSelection",
                    icon = "mdi mdi-send-outline",
                    colorClass = "primary"
                });
                return actions;
            }

            HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(v1.certificateID.Value);
            if (cert == null || cert.certificateStatusID == 2)
                return actions;

            // Ο επιθεωρητής απέρριψε την ανάθεση → επιλογή νέου επιθεωρητή
            if (cert.certificateStatusID == 24)
            {
                actions.Add(new PendingActionViewModel
                {
                    title = "Η ανάθεση απορρίφθηκε από τον επιθεωρητή",
                    description = "Επιλέξτε νέο επιθεωρητή για να συνεχιστεί η διαδικασία αξιολόγησης.",
                    link = "/InspectorSelection",
                    icon = "mdi mdi-account-cancel-outline",
                    colorClass = "danger"
                });
                return actions;
            }

            decimal certID = cert.certificateID;
            HotelCriteria v3 = unitOfWork.HotelCriteriaRepository
                .Get(x => x.certificateID == certID && x.version == 3)
                .FirstOrDefault();

            // Αποδοχή τελικής κατάταξης
            if (v3 != null && v3.status == 2)
            {
                actions.Add(new PendingActionViewModel
                {
                    title = "Αποδοχή Τελικής Κατάταξης",
                    description = "Ο επιθεωρητής ολοκλήρωσε την τελική κατάταξη — απαιτείται η αποδοχή σας για την έκδοση της βεβαίωσης.",
                    link = "/InspectorSelection",
                    icon = "mdi mdi-seal",
                    colorClass = "danger"
                });
            }
            // Ο επιθεωρητής όρισε νέα ημερομηνία αυτοψίας
            else if (cert.autopsyDateStatus == 3 && cert.autopsyDateTime.HasValue
                     && cert.autopsyDateTime.Value.Date >= DateTime.Today)
            {
                actions.Add(new PendingActionViewModel
                {
                    title = "Νέα ημερομηνία αυτοψίας",
                    description = "Ο επιθεωρητής όρισε νέα ημερομηνία αυτοψίας: " + cert.autopsyDateTime.Value.ToString("dd/MM/yyyy"),
                    link = "/InspectorSelection",
                    icon = "mdi mdi-calendar-alert",
                    colorClass = "info"
                });
            }

            return actions;
        }

        // ── Σημειώσεις ημερολογίου για σήμερα/αύριο ─────────────────────
        private List<PendingActionViewModel> GetCalendarNoteActions()
        {
            var actions = new List<PendingActionViewModel>();
            string uname = User.Identity.Name;
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            List<UserCalendarNote> notes = unitOfWork.UserCalendarNoteRepository
                .Get(x => x.userName == uname && x.isDone == false
                       && (x.noteDate == today || x.noteDate == tomorrow))
                .ToList();

            foreach (var n in notes.OrderBy(x => x.noteDate))
            {
                bool isToday = n.noteDate.Date == today;
                actions.Add(new PendingActionViewModel
                {
                    title = "Σημείωση: " + n.title,
                    description = (isToday ? "Για σήμερα" : "Για αύριο") + " (" + n.noteDate.ToString("dd/MM/yyyy") + ")",
                    link = "/Calendar",
                    icon = "mdi mdi-note-text-outline",
                    colorClass = isToday ? "warning" : "info"
                });
            }

            return actions;
        }

        // ── Εκκρεμότητες Επιθεωρητή ─────────────────────────────────────
        private List<PendingActionViewModel> GetInspectorActions(decimal inspectorID)
        {
            var actions = new List<PendingActionViewModel>();

            // Ενεργές αναθέσεις του επιθεωρητή
            List<HotelierCertificate> certs = unitOfWork.HotelierCertificateRepository
                .Get(x => x.tee_inspectorID == inspectorID && x.certificateTypeID == 84 && x.certificateStatusID == 23)
                .ToList();

            if (certs.Count == 0) return actions;

            // Τίτλοι ξενοδοχείων από το view
            string sql = "Select * from V_TEE_Certificates_Inspector where UserName = @UserName";
            List<CertificateViewModel> viewRows = unitOfWork.context.Database
                .SqlQuery<CertificateViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .ToList();
            Func<decimal, string> hotelTitle = certID =>
            {
                var row = viewRows.FirstOrDefault(x => x.certificateID == certID);
                return row != null ? row.hotelTitle : "#" + certID;
            };

            // Όλες οι εκδόσεις κριτηρίων των αναθέσεων (batch)
            List<decimal> certIds = certs.Select(x => x.certificateID).ToList();
            List<HotelCriteria> allCrits = unitOfWork.HotelCriteriaRepository
                .Get(x => x.certificateID.HasValue && certIds.Contains(x.certificateID.Value))
                .ToList();

            foreach (var cert in certs)
            {
                HotelCriteria v2 = allCrits.FirstOrDefault(x => x.certificateID == cert.certificateID && x.version == 2);
                HotelCriteria v3 = allCrits.FirstOrDefault(x => x.certificateID == cert.certificateID && x.version == 3);

                // Απόρριψη τελικής κατάταξης από ξενοδόχο → διόρθωση
                // (μόνο όταν υπάρχει σχόλιο απόρριψης ΤΕΛΙΚΗΣ ΚΑΤΑΤΑΞΗΣ — όχι απόρριψη ανάθεσης)
                if (v3 != null && v3.status == 1 && Utils.CertificateNotes.HasFinalRejection(cert.notes))
                {
                    actions.Add(new PendingActionViewModel
                    {
                        title = "Απόρριψη τελικής κατάταξης — " + hotelTitle(cert.certificateID),
                        description = "Ο ξενοδόχος απέρριψε την τελική κατάταξη. Δείτε το σχόλιό του και υποβάλετε διορθωμένη.",
                        link = "/Certificate?certId=" + cert.certificateID,
                        icon = "mdi mdi-comment-alert-outline",
                        colorClass = "danger"
                    });
                    continue;
                }

                // Νέα ανάθεση: εκκρεμεί επιβεβαίωση ημερομηνίας
                if (cert.autopsyDateStatus == 1)
                {
                    actions.Add(new PendingActionViewModel
                    {
                        title = "Νέα ανάθεση — " + hotelTitle(cert.certificateID),
                        description = "Επιβεβαιώστε ή αλλάξτε την προτεινόμενη ημερομηνία αυτοψίας" +
                                      (cert.autopsyDateTime.HasValue ? " (" + cert.autopsyDateTime.Value.ToString("dd/MM/yyyy") + ")" : "") + ".",
                        link = "/Certificate?certId=" + cert.certificateID,
                        icon = "mdi mdi-calendar-question",
                        colorClass = "primary"
                    });
                    continue;
                }

                // Ήρθε η ημερομηνία αυτοψίας και δεν έχει υποβληθεί η v2
                if (cert.autopsyDateTime.HasValue && cert.autopsyDateTime.Value.Date <= DateTime.Today
                    && (v2 == null || v2.status != 2))
                {
                    actions.Add(new PendingActionViewModel
                    {
                        title = "Εκκρεμεί αυτοψία — " + hotelTitle(cert.certificateID),
                        description = "Η ημερομηνία αυτοψίας (" + cert.autopsyDateTime.Value.ToString("dd/MM/yyyy") + ") έχει φτάσει.",
                        link = "/Certificate?certId=" + cert.certificateID,
                        icon = "mdi mdi-clipboard-clock-outline",
                        colorClass = "warning"
                    });
                }
            }

            return actions;
        }
    }
}
