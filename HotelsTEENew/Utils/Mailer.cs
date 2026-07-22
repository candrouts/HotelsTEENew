using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Web;
using System.Web.Configuration;

namespace HotelsTEE.Utils
{
    public class Mailer
    {
        // Χτίζει τον SmtpClient από τα App Settings (smtp.*) — έτσι τα credentials
        // ζουν στα Azure App Settings του κάθε Web App, όχι στο Web.config (git).
        // Αν το smtp.host είναι κενό, γυρνά default client που διαβάζει το
        // <system.net><mailSettings> (fallback για local dev).
        private static SmtpClient BuildSmtpClient()
        {
            string host = WebConfigurationManager.AppSettings["smtp.host"];
            if (string.IsNullOrWhiteSpace(host))
                return new SmtpClient();

            SmtpClient sc = new SmtpClient(host);

            string port = WebConfigurationManager.AppSettings["smtp.port"];
            int portNum;
            if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out portNum))
                sc.Port = portNum;

            string ssl = WebConfigurationManager.AppSettings["smtp.enableSsl"];
            sc.EnableSsl = ssl == "1" || string.Equals(ssl, "true", StringComparison.OrdinalIgnoreCase);

            string user = WebConfigurationManager.AppSettings["smtp.user"];
            string pass = WebConfigurationManager.AppSettings["smtp.password"];
            if (!string.IsNullOrWhiteSpace(user))
            {
                sc.UseDefaultCredentials = false;
                sc.Credentials = new NetworkCredential(user, pass);
            }

            return sc;
        }

        // Θέτει τον αποστολέα από το smtp.from (App Settings) αν δεν έχει οριστεί.
        private static void EnsureFrom(MailMessage mail)
        {
            if (mail.From == null)
            {
                string from = WebConfigurationManager.AppSettings["smtp.from"];
                if (!string.IsNullOrWhiteSpace(from))
                    mail.From = new MailAddress(from);
            }
        }

        // Απευθείας αποστολή στον πραγματικό παραλήπτη, χωρίς test-mode εκτροπή
        // (sendEmailClearTrueRecipient) και χωρίς BCC — για emails λογαριασμών
        // (ενεργοποίηση, επαναφορά κωδικού) που πρέπει να φτάσουν ΜΟΝΟ στον χρήστη.
        public static bool SendEmailDirect(MailMessage mail)
        {
            return SendEmail(mail, true);
        }

        public static bool SendEmail(MailMessage mail)
        {
            return SendEmail(mail, false);
        }

        private static bool SendEmail(MailMessage mail, bool direct)
        {
            try
            {
                string header = string.Empty;

                header += "<table width=\"700\" cellpadding=\"0\" cellspacing=\"0\" bgcolor=\"#EEEEEE\" style=\"background-color:#EEEEEE;width:700px;\" >";

                header += "<tr><td align=\"center\">";

                header += "<img src=\"https://services.grhotels.gr/images/eservices-header-email.jpg\" width=\"700\" style=\"width:700px;\">";
                header += "<table width=\"700\" cellpadding=\"4\" cellspacing=\"4\" bgcolor=\"#EEEEEE\" style=\"background-color:#EEEEEE;width:700px;\" >";

                header += "<tr><td align=\"center\">";
                header += "<table width=\"100%\" bgcolor=\"#FFFFFF\" style=\"background-color:#FFFFFF;width:100%;\" align=\"center\">";
                header += "<tr><td bgcolor=\"#ffffff\"><br /><br />";


                string footer = string.Empty;
                footer += "<br /><br /></td></tr>";
                footer += "</table>";
                footer += "</td></tr>";

                footer += "<tr><td bgcolor=\"#EEEEEE\" font-size=\"10\" font-color=\"#222222\" style=\"background-color:#EEEEEE;font-size:10px;color:#222222;\">";
                footer += @"ΞΕΝΟΔΟΧΕΙΑΚΟ ΕΠΙΜΕΛΗΤΗΡΙΟ ΤΗΣ ΕΛΛΑΔΟΣ | Σταδίου 24 – Αθήνα 105 64 | Τηλ: 213 2169900 | Fax: 210 3225449 | E-mail: info@grhotels.gr";
                footer += "</td></tr>";

                footer += "<tr><td bgcolor=\"#EEEEEE\" font-size=\"9\" font-color=\"#6D6D6D\" style=\"background-color:#EEEEEE;font-size:9px;color:#6D6D6D;\">";
                footer += "<hr style=\"color:#919090; background-color:#919090; height:1px; border:none;\"><br />Αυτό το ηλεκτρονικό μήνυμα περιέχει εμπιστευτικό υλικό για αποκλειστική χρήση του προοριζόμενου παραλήπτη. Οποιαδήποτε αναθεώρηση ή διανομή από άλλους είναι αυστηρά απαγορευμένη. Εάν δεν είστε ο προοριζόμενος παραλήπτης παρακαλώ ελάτε σε επαφή με τον αποστολέα και διαγράψτε όλα τα αντίγραφα.";
                footer += "</td></tr>";

                footer += "</table>";
                footer += "</td></tr>";

                footer += "</table>";
                mail.Body = header + mail.Body + footer;
                mail.IsBodyHtml = true;

                string sendEmailClearTrueRecipient = WebConfigurationManager.AppSettings["sendEmailClearTrueRecipient"].ToString();
                if (!direct && sendEmailClearTrueRecipient == "1")
                {
                    mail.To.Clear();
                }

                string sendEmailBCC = WebConfigurationManager.AppSettings["sendEmailBCC"].ToString();
                if (!direct && sendEmailBCC == "1")
                {
                    string bccEmail1 = WebConfigurationManager.AppSettings["bccEmail1"].ToString();
                    if (bccEmail1.Length > 0) mail.Bcc.Add(bccEmail1);
                    string bccEmail2 = WebConfigurationManager.AppSettings["bccEmail2"].ToString();
                    if (bccEmail2.Length > 0) mail.Bcc.Add(bccEmail2);
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;


                EnsureFrom(mail);
                SmtpClient sc = BuildSmtpClient();
                sc.Send(mail);
                return true;
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "Mailer.cs");
                return false;
            }
        }



        public static bool SendEmailException(Exception ex, HttpSessionStateBase session = null, string page = null)
        {
            string text = "";
            if (session != null)
            {
                string hotelID = (string)session["hotelID"];
                string rentRoomID = (string)session["rentRoomID"];
                string auditorCompanyID = (string)session["auditorCompanyID"];
                string exploitingCompanyID = (string)session["exploitingCompanyID"];
                string employeeID = (string)session["employeeID"];

                text += "Page : " + page + "<br/>";
                text += "hotelID : " + hotelID + "<br/>";
                text += "rentRoomID : " + rentRoomID + "<br/>";
                text += "exploitingCompanyID : " + exploitingCompanyID + "<br/>";
                text += "auditorCompanyID : " + auditorCompanyID + "<br/>";
                text += "employeeID : " + employeeID + "<br/>";


            }

            MailMessage mail = new MailMessage();
            mail.Subject = "Certification catch exception runtime error " + System.Environment.MachineName;
            mail.IsBodyHtml = true;


            mail.Body = "<strong><u>Exception</u></strong><br />";
            if (text != null)
            {
                mail.Body += text;
            }
            mail.Body += "<br/><strong>Message:</strong>" + ex.Message;
            mail.Body += "<br/><strong>Data:</strong>" + ex.Data;
            mail.Body += "<br/><strong>Source:</strong>" + ex.Source;
            mail.Body += "<br/><strong>TargetSite:</strong>" + ex.TargetSite;
            mail.Body += "<br/><strong>StackTrace:</strong>" + ex.StackTrace;
            mail.Body += "<br/><strong>HelpLink:</strong>" + ex.HelpLink;

            if (ex.InnerException != null)
            {
                mail.Body += "<br /><hr /><br /><strong><u>Inner Exception</u></strong><br />";
                mail.Body += "<br/><strong>Message:</strong>" + ex.InnerException.Message;
                mail.Body += "<br/><strong>Data:</strong>" + ex.InnerException.Data;
                mail.Body += "<br/><strong>Source:</strong>" + ex.InnerException.Source;
                mail.Body += "<br/><strong>TargetSite:</strong>" + ex.InnerException.TargetSite;
                mail.Body += "<br/><strong>StackTrace:</strong>" + ex.InnerException.StackTrace;
                mail.Body += "<br/><strong>HelpLink:</strong>" + ex.InnerException.HelpLink;
            }


            mail.To.Add("tolisgrigorakis@gmail.com");
            EnsureFrom(mail);
            SmtpClient sc = BuildSmtpClient();
            try
            {
                sc.Send(mail);

            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "Mailer.cs");
                // Mailer.SendEmailException(e);
            }


            return true;

        }


    }
}