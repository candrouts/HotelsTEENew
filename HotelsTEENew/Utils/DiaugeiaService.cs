using HotelsTEE.DAL;
using HotelsTEE.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Configuration;

namespace HotelsTEE.Utils
{
    public class DiaugeiaResponseModel
    {
        public string ada { get; set; }
        public string url { get; set; }
        public string documentUrl { get; set; }
    }

    public class DiaugeiaResult
    {
        public bool enabled { get; set; }
        public bool success { get; set; }
        public string ada { get; set; }
        public string message { get; set; }
    }

    // Ανάρτηση βεβαίωσης στη Διαύγεια. ΑΝΕΝΕΡΓΟ μέχρι το go-live (diaugeia.enabled=1).
    public static class DiaugeiaService
    {
        private static string Cfg(string key) { try { return WebConfigurationManager.AppSettings[key]; } catch { return null; } }

        public static bool IsEnabled()
        {
            string v = Cfg("diaugeia.enabled");
            return v == "1" || (v != null && v.ToLower() == "true");
        }

        // Αναρτά το έγγραφο (PDF bytes) στη Διαύγεια, αποθηκεύει ΑΔΑ/urls στο certificate,
        // κατεβάζει το σφραγισμένο αρχείο και το αποθηκεύει ως επίσημο έγγραφο.
        public static DiaugeiaResult Post(UnitOfWork uow, HotelierCertificate cert, byte[] documentBytes)
        {
            var result = new DiaugeiaResult { enabled = IsEnabled(), success = false };

            if (!result.enabled)
                return result;   // ανενεργό — δεν κάνουμε τίποτα

            try
            {
                string url = Cfg("diaugeia.url") + "/decisions";
                string username = Cfg("diaugeia.username");
                string password = Cfg("diaugeia.password");
                string orgID = Cfg("diaugeia.orgID");
                string signerID = Cfg("diaugeia.signerID");
                string unitID = Cfg("diaugeia.unitID");
                string subject = Cfg("diaugeia.subject");
                string decisionTypeID = Cfg("diaugeia.decisionTypeID");
                string thematicCategoryID = Cfg("diaugeia.thematicCategoryID") ?? "40";
                string notifyEmail = Cfg("diaugeia.notifyEmail") ?? "";

                // Πλήρης αριθμός βεβαίωσης (10ψήφιος) ως πρωτόκολλο
                string certNumber = CertificateDocService.EnsureCertNumber(uow, cert);
                string base64String = Convert.ToBase64String(documentBytes, 0, documentBytes.Length);
                string issueDate = (cert.issueDateTime ?? DateTime.Now).ToString("yyyy-MM-dd");

                string jsonToPost = @"{""publish"": true,
                    ""protocolNumber"": ""[certificateCode]"",
                    ""subject"": ""[subject]"",
                    ""decisionTypeId"": ""[decisionTypeID]"",
                    ""issueDate"": ""[date]"",
                    ""organizationId"": ""[orgID]"",
                    ""signerIds"": [""[signerID]""],
                    ""unitIds"": [""[unitID]""],
                    ""thematicCategoryIds"": [""[thematicID]""],
                    ""extraFieldValues"": { ""relatedDecisions"": [], ""documentType"": ""ΠΡΑΞΗ"" },
                    ""actions"": [{ ""name"": ""notifyRecipients"", ""args"": [""[notify]""] }],
                    ""decisionDocumentBase64"": ""[BASE-64 ENCODED FILE]"" }";

                jsonToPost = jsonToPost.Replace("[certificateCode]", certNumber)
                                       .Replace("[subject]", subject)
                                       .Replace("[decisionTypeID]", decisionTypeID)
                                       .Replace("[date]", issueDate)
                                       .Replace("[orgID]", orgID)
                                       .Replace("[signerID]", signerID)
                                       .Replace("[unitID]", unitID)
                                       .Replace("[thematicID]", thematicCategoryID)
                                       .Replace("[notify]", notifyEmail)
                                       .Replace("[BASE-64 ENCODED FILE]", base64String);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                string credentials = username + ":" + password;
                request.Headers["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                request.KeepAlive = true;
                request.Timeout = 300000;
                request.Method = "POST";
                request.ContentType = "application/json";
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var sw = new StreamWriter(request.GetRequestStream()))
                    sw.Write(jsonToPost);

                string responseStr;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (var sr = new StreamReader(response.GetResponseStream()))
                    responseStr = sr.ReadToEnd();

                DiaugeiaResponseModel json = JsonConvert.DeserializeObject<DiaugeiaResponseModel>(responseStr);

                cert.ada = json.ada;
                cert.diaugeiaDocumentUrl = json.documentUrl;
                cert.diaugeiaUrl = json.url;
                uow.HotelierCertificateRepository.Update(cert);
                uow.Save();

                // Κατέβασμα του σφραγισμένου αρχείου (με ΑΔΑ) και αποθήκευση ως επίσημο έγγραφο
                try
                {
                    byte[] stamped = DownloadFile(json.documentUrl);
                    if (stamped != null && stamped.Length > 0)
                    {
                        CertificateFile f = new CertificateFile
                        {
                            certificateFile = stamped,
                            title = "Βεβαίωση (Διαύγεια) " + certNumber + " - ΑΔΑ " + json.ada,
                            fileType = "pdf",
                            creationDateTime = DateTime.Now
                        };
                        uow.CertificateFileRepository.Insert(f);
                        uow.Save();
                        cert.certificateFileID = f.certificateFileID;
                        uow.HotelierCertificateRepository.Update(cert);
                        uow.Save();
                    }
                }
                catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "DiaugeiaService.cs"); /* το κατέβασμα δεν είναι κρίσιμο */ }

                result.success = true;
                result.ada = json.ada;
                return result;
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "DiaugeiaService.cs");
                try { Mailer.SendEmailException(e); } catch { }
                result.message = e.Message;
                return result;
            }
        }

        private static byte[] DownloadFile(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new WebClient())
                return client.DownloadData(url);
        }
    }
}
