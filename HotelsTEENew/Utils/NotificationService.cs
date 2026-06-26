using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net.Mail;
using System.Web.Configuration;

namespace HotelsTEE.Utils
{
    // Κεντρικός μηχανισμός αποστολής ειδοποιήσεων.
    // Τα transition points καλούν Fire(eventKey, certificateID, tokens).
    public static class NotificationService
    {
        // recipientType: 1=Ξενοδόχος, 2=Ανατεθειμένος Επιθεωρητής, 3=Admin, 4=Custom
        public static void Fire(string eventKey, decimal? certificateID, Dictionary<string, string> tokens)
        {
            // Ποτέ να μην ρίξει την κύρια ροή — όλα μέσα σε try/catch.
            try
            {
                using (var uow = new UnitOfWork())
                {
                    NotificationTemplate tmpl = uow.NotificationTemplateRepository
                        .Get(x => x.eventKey == eventKey).FirstOrDefault();

                    // Αν δεν υπάρχει ρυθμισμένο template ή είναι ανενεργό → δεν στέλνουμε
                    if (tmpl == null || !tmpl.isActive)
                        return;

                    string toEmail = ResolveRecipient(uow, tmpl, certificateID);
                    if (string.IsNullOrWhiteSpace(toEmail))
                    {
                        Log(uow, eventKey, null, tmpl.subject, false, "Δεν βρέθηκε παραλήπτης.");
                        return;
                    }

                    string subject = Render(tmpl.subject, tokens);
                    string body = Render(tmpl.body, tokens);

                    bool ok;
                    string error = null;
                    try
                    {
                        MailMessage mail = new MailMessage();
                        mail.Subject = subject;
                        mail.Body = body;
                        mail.IsBodyHtml = true;
                        mail.To.Add(toEmail);
                        ok = Mailer.SendEmail(mail);
                        if (!ok) error = "Αποτυχία αποστολής (SMTP).";
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        error = ex.Message;
                    }

                    Log(uow, eventKey, toEmail, subject, ok, error);
                }
            }
            catch (Exception)
            {
                // swallow — οι ειδοποιήσεις δεν μπλοκάρουν ποτέ τη ροή
            }
        }

        private static string ResolveRecipient(UnitOfWork uow, NotificationTemplate tmpl, decimal? certificateID)
        {
            switch (tmpl.recipientType)
            {
                case 4: // Custom
                    return tmpl.customEmail;

                case 3: // Admin (από Web.config)
                    return SafeAppSetting("adminNotificationEmail");

                case 2: // Ανατεθειμένος επιθεωρητής
                    if (!certificateID.HasValue) return null;
                    HotelierCertificate cert = uow.HotelierCertificateRepository.GetByID(certificateID.Value);
                    if (cert == null || !cert.tee_inspectorID.HasValue) return null;
                    Inspector insp = uow.InspectorRepository.GetByID(cert.tee_inspectorID.Value);
                    return insp?.email;

                case 1: // Ξενοδόχος
                default:
                    if (!certificateID.HasValue) return null;
                    HotelierCertificate c = uow.HotelierCertificateRepository.GetByID(certificateID.Value);
                    if (c == null) return null;
                    string sql = "Select top 1 email from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                    HotelDetailsViewModel hd = uow.context.Database
                        .SqlQuery<HotelDetailsViewModel>(sql,
                            new SqlParameter("@hotelID", c.hotelID ?? ""),
                            new SqlParameter("@companyID", c.exploitingCompanyID ?? ""))
                        .FirstOrDefault();
                    return hd?.email;
            }
        }

        private static string Render(string template, Dictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(template)) return "";
            string result = template;
            if (tokens != null)
            {
                foreach (var kv in tokens)
                {
                    // τα keys έρχονται ως "hotelName" → token "{hotelName}"
                    result = result.Replace("{" + kv.Key + "}", kv.Value ?? "");
                }
            }
            return result;
        }

        private static void Log(UnitOfWork uow, string eventKey, string toEmail, string subject, bool success, string error)
        {
            try
            {
                uow.NotificationLogRepository.Insert(new NotificationLog
                {
                    eventKey = eventKey,
                    toEmail = toEmail,
                    subject = subject,
                    sentDateTime = DateTime.Now,
                    success = success,
                    error = error
                });
                uow.Save();
            }
            catch (Exception) { }
        }

        private static string SafeAppSetting(string key)
        {
            try { return WebConfigurationManager.AppSettings[key]; }
            catch { return null; }
        }

        // Βοηθητικό: όνομα καταλύματος από certificate (για το {hotelName})
        public static string HotelName(decimal certificateID)
        {
            try
            {
                using (var uow = new UnitOfWork())
                {
                    HotelierCertificate c = uow.HotelierCertificateRepository.GetByID(certificateID);
                    if (c == null) return "";
                    string sql = "Select top 1 hotelTitle from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                    HotelDetailsViewModel hd = uow.context.Database
                        .SqlQuery<HotelDetailsViewModel>(sql,
                            new SqlParameter("@hotelID", c.hotelID ?? ""),
                            new SqlParameter("@companyID", c.exploitingCompanyID ?? ""))
                        .FirstOrDefault();
                    return hd?.hotelTitle ?? "";
                }
            }
            catch { return ""; }
        }

        // Βοηθητικό: absolute link προς σελίδα της πλατφόρμας (βάση από Web.config "siteBaseUrl")
        public static string Link(string relativePath)
        {
            string baseUrl = SafeAppSetting("siteBaseUrl") ?? "";
            baseUrl = baseUrl.TrimEnd('/');
            if (string.IsNullOrEmpty(relativePath)) return baseUrl;
            if (!relativePath.StartsWith("/")) relativePath = "/" + relativePath;
            return baseUrl + relativePath;
        }
    }
}
