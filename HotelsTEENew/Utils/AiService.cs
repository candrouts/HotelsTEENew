using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace HotelsTEE.Utils
{
    // ════════════════════════════════════════════════════════════════
    //  AI υπηρεσίες (ai branch / greencertai) — Azure OpenAI + Document
    //  Intelligence. Όλα guarded από το ai.enabled (App Settings στο
    //  Azure Web App υπερισχύουν του Web.config).
    // ════════════════════════════════════════════════════════════════
    public static class AiService
    {
        private static string Cfg(string key)
        {
            return ConfigurationManager.AppSettings[key] ?? "";
        }

        public static bool IsEnabled()
        {
            return Cfg("ai.enabled") == "1"
                && !string.IsNullOrEmpty(Cfg("ai.openai.endpoint"))
                && !string.IsNullOrEmpty(Cfg("ai.openai.key"));
        }

        static AiService()
        {
            // Azure endpoints απαιτούν TLS 1.2 (default του .NET 4.8, αλλά ρητά για σιγουριά)
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        // ── Azure OpenAI: chat completion ────────────────────────────────
        // Επιστρέφει το κείμενο απάντησης του μοντέλου, ή null σε σφάλμα.
        public static string Chat(string systemPrompt, string userMessage, decimal temperature = 0.2m, int maxTokens = 1500)
        {
            if (!IsEnabled()) return null;

            try
            {
                string endpoint = Cfg("ai.openai.endpoint").TrimEnd('/');
                string deployment = Cfg("ai.openai.deployment");
                string url = endpoint + "/openai/deployments/" + deployment +
                             "/chat/completions?api-version=2024-06-01";

                var payload = new
                {
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt ?? "" },
                        new { role = "user", content = userMessage ?? "" }
                    },
                    temperature = temperature,
                    max_tokens = maxTokens
                };

                string responseJson = PostJson(url, JsonConvert.SerializeObject(payload),
                    "api-key", Cfg("ai.openai.key"));
                if (responseJson == null) return null;

                JObject o = JObject.Parse(responseJson);
                return (string)o.SelectToken("choices[0].message.content");
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "AiService.Chat");
                return null;
            }
        }

        // ── Azure AI Document Intelligence: εξαγωγή κειμένου (prebuilt-read)
        // Δέχεται τα bytes του εγγράφου (pdf/εικόνα), επιστρέφει το κείμενο ή null.
        public static string ExtractText(byte[] fileBytes, string contentType = "application/pdf")
        {
            string endpoint = Cfg("ai.docintel.endpoint").TrimEnd('/');
            string key = Cfg("ai.docintel.key");
            if (Cfg("ai.enabled") != "1" || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
                return null;

            try
            {
                string url = endpoint + "/documentintelligence/documentModels/prebuilt-read:analyze?api-version=2024-11-30";

                // 1) Υποβολή — επιστρέφει Operation-Location για polling
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = contentType;
                req.Headers.Add("Ocp-Apim-Subscription-Key", key);
                req.Timeout = 60000;
                using (var s = req.GetRequestStream())
                    s.Write(fileBytes, 0, fileBytes.Length);

                string opLocation;
                using (var resp = (HttpWebResponse)req.GetResponse())
                    opLocation = resp.Headers["Operation-Location"];
                if (string.IsNullOrEmpty(opLocation)) return null;

                // 2) Polling μέχρι να ολοκληρωθεί η ανάλυση (max ~30s)
                for (int i = 0; i < 30; i++)
                {
                    Thread.Sleep(1000);

                    HttpWebRequest poll = (HttpWebRequest)WebRequest.Create(opLocation);
                    poll.Method = "GET";
                    poll.Headers.Add("Ocp-Apim-Subscription-Key", key);
                    poll.Timeout = 30000;

                    string pollJson;
                    using (var resp = (HttpWebResponse)poll.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        pollJson = reader.ReadToEnd();

                    JObject o = JObject.Parse(pollJson);
                    string status = (string)o["status"];
                    if (status == "succeeded")
                        return (string)o.SelectToken("analyzeResult.content");
                    if (status == "failed")
                        return null;
                }
                return null;   // timeout
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex, "AiService.ExtractText");
                return null;
            }
        }

        // ── Κοινό POST JSON helper ───────────────────────────────────────
        private static string PostJson(string url, string json, string authHeader, string authValue)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json";
            req.Headers.Add(authHeader, authValue);
            req.Timeout = 120000;

            byte[] body = Encoding.UTF8.GetBytes(json);
            using (var s = req.GetRequestStream())
                s.Write(body, 0, body.Length);

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return reader.ReadToEnd();
            }
            catch (WebException wex)
            {
                // Καταγραφή του response body του σφάλματος (χρήσιμο για 401/429/κλπ)
                string detail = "";
                try
                {
                    using (var r = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                        detail = r.ReadToEnd();
                }
                catch (Exception) { }
                ErrorLogger.Log(new Exception("AI HTTP error: " + wex.Message + " | " + detail, wex), "AiService.PostJson");
                return null;
            }
        }
    }
}
