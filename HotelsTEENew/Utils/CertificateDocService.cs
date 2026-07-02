using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace HotelsTEE.Utils
{
    public class CertificateDocResult
    {
        public bool success { get; set; }
        public string message { get; set; }
        public string html { get; set; }
        public string title { get; set; }
        public byte[] bytes { get; set; }
    }

    public static class CertificateDocService
    {
        // Διαθέσιμα tokens (για το admin UI)
        public static readonly string[] Tokens = new[] {
            "{certNumber}", "{hotelType}", "{hotelName}", "{category}", "{address}",
            "{company}", "{taxNumber}", "{medal}", "{score}", "{inspectorName}",
            "{issueDate}", "{validUntil}", "{today}", "{place}"
        };

        // Προεπιλεγμένο πρότυπο (HTML) — βασισμένο στο Πιστοποιητικό Κατάταξης
        public static string DefaultTemplate()
        {
            return @"
<div style='font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;max-width:800px;margin:0 auto;padding:30px;'>
  <table style='width:100%;border-collapse:collapse;margin-bottom:10px;'>
    <tr>
      <td style='text-align:center;width:50%;'>
        <div style='font-weight:bold;font-size:13px;'>ΕΛΛΗΝΙΚΗ ΔΗΜΟΚΡΑΤΙΑ</div>
      </td>
      <td style='text-align:center;width:50%;'>
        <div style='font-weight:bold;font-size:13px;'>ΞΕΝΟΔΟΧΕΙΑΚΟ ΕΠΙΜΕΛΗΤΗΡΙΟ ΕΛΛΑΔΟΣ</div>
      </td>
    </tr>
  </table>
  <hr/>

  <h1 style='text-align:center;font-size:24px;margin:18px 0 2px;'>Βεβαίωση Περιβαλλοντικής Βιωσιμότητας</h1>
  <h2 style='text-align:center;font-size:18px;margin:0 0 4px;font-weight:bold;'>Τουριστικού Καταλύματος</h2>
  <p style='text-align:center;font-size:12px;color:#444;margin:0 0 18px;'>(Σύστημα Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων)</p>

  <p style='text-align:center;font-weight:bold;font-size:14px;'>Αριθμός Βεβαίωσης: {certNumber}</p>

  <table style='width:100%;font-size:14px;margin:18px 0;'>
    <tr><td style='font-weight:bold;width:42%;padding:3px 0;'>Είδος Καταλύματος:</td><td>{hotelType}</td></tr>
    <tr><td style='font-weight:bold;padding:3px 0;'>Διακριτικός Τίτλος Καταλύματος:</td><td>{hotelName}</td></tr>
    <tr><td style='font-weight:bold;padding:3px 0;'>Κατηγορία Καταλύματος:</td><td>{category}</td></tr>
    <tr><td style='font-weight:bold;padding:3px 0;'>Ταχυδρομική Διεύθυνση:</td><td>{address}</td></tr>
    <tr><td style='font-weight:bold;padding:3px 0;'>Επωνυμία Επιχείρησης:</td><td>{company}</td></tr>
    <tr><td style='font-weight:bold;padding:3px 0;'>Α.Φ.Μ.:</td><td>{taxNumber}</td></tr>
  </table>

  <hr/>
  <p style='font-size:13px;text-align:justify;margin:14px 0;'>
    Το ανωτέρω τουριστικό κατάλυμα, κατόπιν αξιολόγησης των κριτηρίων περιβαλλοντικής βιωσιμότητας
    και βάσει της τεχνικής έκθεσης του επιθεωρητή <b>{inspectorName}</b>, με συνολική βαθμολογία
    <b>{score}/95</b>, κατατάσσεται στη βαθμίδα:
  </p>

  <h1 style='text-align:center;font-size:40px;margin:16px 0;color:#b8860b;letter-spacing:2px;'>{medal}</h1>
  <hr/>

  <table style='width:100%;font-size:14px;margin-top:26px;'>
    <tr>
      <td style='width:55%;'>
        <div><b>Ημερομηνία έκδοσης:</b> {issueDate}</div>
        <div style='margin-top:6px;'><b>Ημερομηνία λήξης ισχύος:</b> {validUntil}</div>
      </td>
      <td style='text-align:center;'>
        <div>{place} {today}</div>
        <div style='margin-top:55px;border-top:1px solid #333;display:inline-block;padding-top:4px;'>Ο/Η Διευθυντής/ντρια</div>
      </td>
    </tr>
  </table>
</div>";
        }

        // Επιστρέφει το ενεργό πρότυπο, ή το default αν δεν υπάρχει
        public static string GetActiveTemplate(UnitOfWork uow)
        {
            CertificateTemplate t = uow.CertificateTemplateRepository
                .Get(x => x.isActive).OrderByDescending(x => x.id).FirstOrDefault();
            return (t != null && !string.IsNullOrWhiteSpace(t.body)) ? t.body : DefaultTemplate();
        }

        // Παράγει τη βεβαίωση, την αποθηκεύει στο CertificateFiles, συνδέει το
        // certificateFileID στην αίτηση και (αν είναι ενεργή) την αναρτά στη Διαύγεια.
        // Idempotent: αν υπάρχει ήδη αρχείο και δεν ζητηθεί force, δεν ξαναπαράγει.
        public static CertificateDocResult IssueAndStore(UnitOfWork uow, decimal certificateID, bool force = false)
        {
            HotelierCertificate cert = uow.HotelierCertificateRepository.GetByID(certificateID);
            if (cert == null)
                return new CertificateDocResult { success = false, message = "Δεν βρέθηκε η αίτηση." };

            if (cert.certificateFileID.HasValue && !force)
                return new CertificateDocResult { success = true, message = "Υπάρχει ήδη." };

            CertificateDocResult doc = BuildForCertificate(uow, certificateID);
            if (!doc.success)
                return doc;

            CertificateFile file = new CertificateFile
            {
                certificateFile = doc.bytes,
                title = doc.title,
                fileType = "html",
                creationDateTime = DateTime.Now
            };
            uow.CertificateFileRepository.Insert(file);
            uow.Save();   // παίρνει το identity

            cert.certificateFileID = file.certificateFileID;
            uow.HotelierCertificateRepository.Update(cert);
            uow.Save();

            // Ανάρτηση στη Διαύγεια — εκτελείται μόνο όταν diaugeia.enabled=1 (go-live)
            try { DiaugeiaService.Post(uow, cert, doc.bytes); }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "CertificateDocService.cs"); /* η ανάρτηση δεν μπλοκάρει την έκδοση/αποθήκευση */ }

            return doc;
        }

        // Χτίζει το HTML της βεβαίωσης για ένα certificate
        public static CertificateDocResult BuildForCertificate(UnitOfWork uow, decimal certificateID)
        {
            var res = new CertificateDocResult { success = false };
            try
            {
                HotelierCertificate cert = uow.HotelierCertificateRepository.GetByID(certificateID);
                if (cert == null) { res.message = "Δεν βρέθηκε η αίτηση."; return res; }
                if (cert.certificateStatusID != 2) { res.message = "Η βεβαίωση δεν έχει εκδοθεί ακόμα."; return res; }

                string sql = "Select top 1 * from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                HotelDetailsViewModel hd = uow.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql,
                        new SqlParameter("@hotelID", cert.hotelID ?? ""),
                        new SqlParameter("@companyID", cert.exploitingCompanyID ?? ""))
                    .FirstOrDefault();

                decimal cid = cert.certificateID;
                HotelCriteria v3 = uow.HotelCriteriaRepository
                    .Get(x => x.certificateID == cid && x.version == 3).FirstOrDefault();

                string medalTitle = "-";
                string score = "-";
                if (v3 != null)
                {
                    score = (v3.totalScore ?? v3.totalPoints).ToString("0.##");
                    if (v3.medalID.HasValue)
                    {
                        Medal m = uow.MedalRepository.GetByID(v3.medalID.Value);
                        if (m != null) medalTitle = m.title;
                    }
                }

                Inspector insp = cert.tee_inspectorID.HasValue
                    ? uow.InspectorRepository.GetByID(cert.tee_inspectorID.Value) : null;

                string certNumber = EnsureCertNumber(uow, cert);

                var tokens = new Dictionary<string, string>
                {
                    { "certNumber", certNumber },
                    { "hotelType", hd != null ? (hd.hotelType ?? "") : "" },
                    { "hotelName", hd != null ? (hd.hotelTitle ?? "") : "" },
                    { "category", hd != null ? (hd.category ?? "") : "" },
                    { "address", hd != null ? ((hd.address ?? "") + " " + (hd.zipCode ?? "") + " " + (hd.municipalityTitle ?? "")).Trim() : "" },
                    { "company", hd != null ? (hd.exploitingCompanyName ?? "") : "" },
                    { "taxNumber", hd != null ? (hd.taxNumber ?? "") : "" },
                    { "medal", medalTitle },
                    { "score", score },
                    { "inspectorName", insp != null ? insp.firstName + " " + insp.lastName : "-" },
                    { "issueDate", cert.issueDateTime.HasValue ? cert.issueDateTime.Value.ToString("dd/MM/yyyy") : "-" },
                    { "validUntil", cert.validityStopDateTime.HasValue ? cert.validityStopDateTime.Value.ToString("dd/MM/yyyy") : "-" },
                    { "today", DateTime.Now.ToString("dd/MM/yyyy") },
                    { "place", "Αθήνα," }
                };

                string template = GetActiveTemplate(uow);
                string html = Render(template, tokens);

                // Πλήρες HTML έγγραφο (UTF-8) έτοιμο για εκτύπωση/αποθήκευση ως PDF
                string full = "<!DOCTYPE html><html lang='el'><head><meta charset='utf-8'>" +
                              "<title>Βεβαίωση " + tokens["hotelName"] + "</title>" +
                              "<style>@media print{.noprint{display:none}}</style></head><body>" +
                              "<div class='noprint' style='text-align:center;margin:10px;'>" +
                              "<button onclick='window.print()'>Εκτύπωση / Αποθήκευση ως PDF</button></div>" +
                              html + "</body></html>";

                res.html = full;
                res.title = "Βεβαίωση Βιωσιμότητας - " + tokens["hotelName"] + " (#" + tokens["certNumber"] + ")";
                res.bytes = new UTF8Encoding(true).GetBytes(full);  // με BOM για σωστά ελληνικά
                res.success = true;
                return res;
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateDocService.cs");
                res.message = e.Message;
                return res;
            }
        }

        // Παράγει (αν δεν υπάρχει) και επιστρέφει τον αριθμό βεβαίωσης:
        // 10 ψηφία = '4' + 5ψήφιος αύξων (certificationNumber, typeID=84) + 4ψήφιο έτος έκδοσης.
        // Η δέσμευση του αύξοντα γίνεται ΑΤΟΜΙΚΑ (UPDLOCK/HOLDLOCK) ώστε δύο ταυτόχρονες
        // εκδόσεις να μην πάρουν ποτέ τον ίδιο αριθμό.
        public static string EnsureCertNumber(UnitOfWork uow, HotelierCertificate cert)
        {
            // Η στήλη certificationNumber είναι varchar(20) και για typeID=84 οι κενές
            // τιμές είναι '' (όχι NULL). Δεσμεύουμε ατομικά τον επόμενο ακέραιο αύξοντα
            // (TRY_CAST για ασφάλεια απέναντι σε μη-αριθμητικές τιμές) και τον αποθηκεύουμε
            // ως string. UPDLOCK/HOLDLOCK ώστε δύο ταυτόχρονες εκδόσεις να μην συγκρουστούν.
            if (string.IsNullOrWhiteSpace(cert.certificationNumber))
            {
                string sql = @"
SET NOCOUNT ON;
DECLARE @next INT;
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM HotelierCertificates WITH (UPDLOCK, HOLDLOCK)
           WHERE certificateID = @id
             AND (certificationNumber IS NULL OR LTRIM(RTRIM(certificationNumber)) = ''))
BEGIN
    SELECT @next = ISNULL(MAX(TRY_CAST(certificationNumber AS INT)), 0) + 1
      FROM HotelierCertificates WITH (UPDLOCK, HOLDLOCK)
      WHERE certificateTypeID = 84 AND TRY_CAST(certificationNumber AS INT) IS NOT NULL;
    UPDATE HotelierCertificates SET certificationNumber = CAST(@next AS VARCHAR(20))
      WHERE certificateID = @id;
END
COMMIT TRANSACTION;
SELECT certificationNumber FROM HotelierCertificates WHERE certificateID = @id;";

                string assigned = uow.context.Database
                    .SqlQuery<string>(sql, new SqlParameter("@id", cert.certificateID))
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(assigned))
                    cert.certificationNumber = assigned;
            }

            long seq;
            if (string.IsNullOrWhiteSpace(cert.certificationNumber)
                || !long.TryParse(cert.certificationNumber.Trim(), out seq))
                return "-";   // ασφάλεια, δεν θα έπρεπε να συμβεί

            int year = cert.issueDateTime.HasValue ? cert.issueDateTime.Value.Year : DateTime.Now.Year;
            return "4" + seq.ToString("00000") + year.ToString("0000");
        }

        private static string Render(string template, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(template)) return "";
            string r = template;
            foreach (var kv in tokens)
                r = r.Replace("{" + kv.Key + "}", kv.Value ?? "");
            return r;
        }
    }
}
