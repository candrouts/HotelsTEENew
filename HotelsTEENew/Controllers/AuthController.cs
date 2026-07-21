using HotelsTEE.DAL;
using HotelsTEE.Models;
using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using System.Web.Security;

namespace HotelsTEE.Controllers
{
    // Ενεργοποίηση λογαριασμού επιθεωρητή (claim από το μητρώο) & επαναφορά κωδικού.
    // Όλα τα μηνύματα προς τον χρήστη είναι ουδέτερα — δεν αποκαλύπτουν αν ένα email
    // υπάρχει στο μητρώο ή αν έχει λογαριασμό (προστασία από user enumeration).
    [AllowAnonymous]
    public class AuthController : Controller
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        private const int RegisterTokenHours = 24;
        private const int ResetTokenMinutes = 60;
        private const int MaxTokensPerEmailPerHour = 3;

        // ─────────────────────────── Σελίδες ───────────────────────────

        public ActionResult Register() { return View(); }

        public ActionResult RecoverPassword() { return View(); }

        public ActionResult Activate(string token)
        {
            AccountToken t = FindValidToken(token, "register");
            if (t == null)
                return View("TokenInvalid");

            Inspector inspector = t.inspectorID.HasValue
                ? unitOfWork.InspectorRepository.Get(x => x.id == t.inspectorID.Value).FirstOrDefault()
                : null;
            if (inspector == null || !inspector.isActive)
                return View("TokenInvalid");

            ViewBag.Token = token;
            ViewBag.FullName = inspector.firstName + " " + inspector.lastName;
            ViewBag.Email = t.email;
            ViewBag.AskPhoneDigits = !string.IsNullOrWhiteSpace(inspector.phone) && inspector.phone.Trim().Length >= 4;
            return View();
        }

        public ActionResult ResetPassword(string token)
        {
            AccountToken t = FindValidToken(token, "reset");
            if (t == null)
                return View("TokenInvalid");

            ViewBag.Token = token;
            ViewBag.Email = t.email;
            return View();
        }

        // ─────────────────────── JSON ενέργειες ────────────────────────

        // Αίτημα ενεργοποίησης: δίνει email, στέλνουμε σύνδεσμο αν αντιστοιχεί
        // σε ενεργό επιθεωρητή του μητρώου χωρίς λογαριασμό.
        [HttpPost]
        public ActionResult RegisterRequest(string email)
        {
            try
            {
                email = (email ?? "").Trim();
                if (email.Length < 5 || !email.Contains("@"))
                    return Json(new { success = false, message = "Παρακαλούμε εισάγετε έγκυρο email." });

                if (!RateLimitOk(email))
                    return Json(new { success = true }); // ουδέτερα και εδώ

                Inspector inspector = unitOfWork.InspectorRepository
                    .Get(x => x.isActive && x.email != null && x.email.ToLower() == email.ToLower())
                    .FirstOrDefault();

                if (inspector != null && Membership.GetUser(inspector.email.Trim()) == null)
                    SendTokenEmail(inspector.email.Trim(), inspector.id, "register");

                return Json(new { success = true });
            }
            catch (Exception exLog)
            {
                HotelsTEE.Utils.ErrorLogger.Log(exLog, "AuthController.cs");
                return Json(new { success = false, message = "Παρουσιάστηκε σφάλμα. Προσπαθήστε ξανά." });
            }
        }

        // Ολοκλήρωση ενεργοποίησης: token + (προαιρετικά) 4 τελευταία ψηφία κινητού + κωδικός
        [HttpPost]
        public ActionResult ActivateSubmit(string token, string phoneDigits, string password)
        {
            try
            {
                AccountToken t = FindValidToken(token, "register");
                if (t == null)
                    return Json(new { success = false, message = "Ο σύνδεσμος δεν είναι πλέον έγκυρος. Ζητήστε νέο από τη σελίδα εγγραφής." });

                Inspector inspector = t.inspectorID.HasValue
                    ? unitOfWork.InspectorRepository.Get(x => x.id == t.inspectorID.Value).FirstOrDefault()
                    : null;
                if (inspector == null || !inspector.isActive)
                    return Json(new { success = false, message = "Ο σύνδεσμος δεν είναι πλέον έγκυρος." });

                // Δεύτερος έλεγχος ταυτοποίησης: 4 τελευταία ψηφία δηλωμένου κινητού
                string phone = (inspector.phone ?? "").Trim();
                if (phone.Length >= 4)
                {
                    string expected = phone.Substring(phone.Length - 4);
                    if ((phoneDigits ?? "").Trim() != expected)
                        return Json(new { success = false, message = "Τα ψηφία δεν συμφωνούν με το δηλωμένο τηλέφωνο του μητρώου." });
                }

                string pwdError = ValidatePassword(password);
                if (pwdError != null)
                    return Json(new { success = false, message = pwdError });

                string userName = t.email;
                if (Membership.GetUser(userName) != null)
                    return Json(new { success = false, message = "Υπάρχει ήδη λογαριασμός για αυτό το email. Χρησιμοποιήστε την επαναφορά κωδικού." });

                MembershipCreateStatus status;
                Membership.CreateUser(userName, password, userName, null, null, true, out status);
                if (status != MembershipCreateStatus.Success)
                {
                    HotelsTEE.Utils.ErrorLogger.Log(new Exception("CreateUser failed: " + status + " for " + userName), "AuthController.cs");
                    return Json(new { success = false, message = "Δεν ήταν δυνατή η δημιουργία λογαριασμού. Προσπαθήστε ξανά." });
                }

                // Δέσιμο με το μητρώο: role=10 + tee_inspectorID στο aspnet_Users
                unitOfWork.context.Database.ExecuteSqlCommand(
                    "UPDATE aspnet_Users SET role = 10, tee_inspectorID = @inspectorID WHERE LoweredUserName = LOWER(@userName)",
                    new System.Data.SqlClient.SqlParameter("@inspectorID", inspector.id),
                    new System.Data.SqlClient.SqlParameter("@userName", userName));

                MarkTokenUsed(t);

                return Json(new { success = true });
            }
            catch (Exception exLog)
            {
                HotelsTEE.Utils.ErrorLogger.Log(exLog, "AuthController.cs");
                return Json(new { success = false, message = "Παρουσιάστηκε σφάλμα. Προσπαθήστε ξανά." });
            }
        }

        // Αίτημα επαναφοράς κωδικού (μόνο λογαριασμοί επιθεωρητών, role=10)
        [HttpPost]
        public ActionResult RecoverRequest(string email)
        {
            try
            {
                email = (email ?? "").Trim();
                if (email.Length < 5 || !email.Contains("@"))
                    return Json(new { success = false, message = "Παρακαλούμε εισάγετε έγκυρο email." });

                if (!RateLimitOk(email))
                    return Json(new { success = true });

                var role = unitOfWork.context.Database.SqlQuery<int?>(
                    "SELECT role FROM aspnet_Users WHERE LoweredUserName = LOWER(@userName)",
                    new System.Data.SqlClient.SqlParameter("@userName", email)).FirstOrDefault();

                if (role == 10 && Membership.GetUser(email) != null)
                    SendTokenEmail(email, null, "reset");

                return Json(new { success = true });
            }
            catch (Exception exLog)
            {
                HotelsTEE.Utils.ErrorLogger.Log(exLog, "AuthController.cs");
                return Json(new { success = false, message = "Παρουσιάστηκε σφάλμα. Προσπαθήστε ξανά." });
            }
        }

        // Ολοκλήρωση επαναφοράς: token + νέος κωδικός (+ ξεκλείδωμα λογαριασμού)
        [HttpPost]
        public ActionResult ResetSubmit(string token, string password)
        {
            try
            {
                AccountToken t = FindValidToken(token, "reset");
                if (t == null)
                    return Json(new { success = false, message = "Ο σύνδεσμος δεν είναι πλέον έγκυρος. Ζητήστε νέο από τη σελίδα «Ξεχάσατε τον κωδικό σας»." });

                string pwdError = ValidatePassword(password);
                if (pwdError != null)
                    return Json(new { success = false, message = pwdError });

                MembershipUser user = Membership.GetUser(t.email);
                if (user == null)
                    return Json(new { success = false, message = "Ο σύνδεσμος δεν είναι πλέον έγκυρος." });

                if (user.IsLockedOut)
                    user.UnlockUser();

                string temp = user.ResetPassword();
                if (!user.ChangePassword(temp, password))
                    return Json(new { success = false, message = "Δεν ήταν δυνατή η αλλαγή κωδικού. Προσπαθήστε ξανά." });

                MarkTokenUsed(t);

                return Json(new { success = true });
            }
            catch (Exception exLog)
            {
                HotelsTEE.Utils.ErrorLogger.Log(exLog, "AuthController.cs");
                return Json(new { success = false, message = "Παρουσιάστηκε σφάλμα. Προσπαθήστε ξανά." });
            }
        }

        // ─────────────────────── Κοινός μηχανισμός ─────────────────────

        // Δημιουργεί token, το αποθηκεύει (hash) και στέλνει το σχετικό email.
        // Χρησιμοποιείται και από το admin «Αποστολή πρόσκλησης».
        public static bool SendTokenEmailFor(UnitOfWork uow, string email, decimal? inspectorID, string purpose, string baseUrl, string ip)
        {
            byte[] raw = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(raw);
            string token = Convert.ToBase64String(raw).Replace("+", "-").Replace("/", "_").TrimEnd('=');

            uow.AccountTokenRepository.Insert(new AccountToken
            {
                email = email,
                inspectorID = inspectorID,
                purpose = purpose,
                tokenHash = Sha256Hex(token),
                expiresAt = purpose == "register"
                    ? DateTime.Now.AddHours(RegisterTokenHours)
                    : DateTime.Now.AddMinutes(ResetTokenMinutes),
                createdIP = ip,
                createdDateTime = DateTime.Now
            });
            uow.Save();

            string link, subject, body;
            if (purpose == "register")
            {
                link = baseUrl + "/Auth/Activate?token=" + token;
                subject = "Ενεργοποίηση λογαριασμού επιθεωρητή — Σύστημα Πιστοποίησης Βιωσιμότητας";
                body = "<p>Αγαπητέ/ή συνεργάτη,</p>" +
                       "<p>Λάβαμε αίτημα ενεργοποίησης λογαριασμού επιθεωρητή για αυτή τη διεύθυνση email στο " +
                       "Σύστημα Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων.</p>" +
                       "<p style='margin:18px 0;'><a href='" + link + "' style='background:#0b5b6e;color:#ffffff;padding:10px 22px;text-decoration:none;border-radius:4px;'>Ενεργοποίηση λογαριασμού</a></p>" +
                       "<p>Ο σύνδεσμος ισχύει για " + RegisterTokenHours + " ώρες και μπορεί να χρησιμοποιηθεί μία φορά.</p>" +
                       "<p style='color:#777;font-size:12px;'>Αν δεν υποβάλατε εσείς το αίτημα, αγνοήστε αυτό το μήνυμα.</p>";
            }
            else
            {
                link = baseUrl + "/Auth/ResetPassword?token=" + token;
                subject = "Επαναφορά κωδικού — Σύστημα Πιστοποίησης Βιωσιμότητας";
                body = "<p>Αγαπητέ/ή συνεργάτη,</p>" +
                       "<p>Λάβαμε αίτημα επαναφοράς κωδικού για τον λογαριασμό σας.</p>" +
                       "<p style='margin:18px 0;'><a href='" + link + "' style='background:#0b5b6e;color:#ffffff;padding:10px 22px;text-decoration:none;border-radius:4px;'>Ορισμός νέου κωδικού</a></p>" +
                       "<p>Ο σύνδεσμος ισχύει για " + ResetTokenMinutes + " λεπτά και μπορεί να χρησιμοποιηθεί μία φορά.</p>" +
                       "<p style='color:#777;font-size:12px;'>Αν δεν υποβάλατε εσείς το αίτημα, αγνοήστε αυτό το μήνυμα — ο κωδικός σας δεν αλλάζει.</p>";
            }

            MailMessage mail = new MailMessage();
            mail.To.Add(email);
            mail.Subject = subject;
            mail.Body = body;
            return HotelsTEE.Utils.Mailer.SendEmail(mail);
        }

        private void SendTokenEmail(string email, decimal? inspectorID, string purpose)
        {
            string baseUrl = Request.Url.Scheme + "://" + Request.Url.Authority;
            SendTokenEmailFor(unitOfWork, email, inspectorID, purpose, baseUrl, Request.UserHostAddress);
        }

        private AccountToken FindValidToken(string token, string purpose)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
                return null;
            string hash = Sha256Hex(token.Trim());
            DateTime now = DateTime.Now;
            return unitOfWork.AccountTokenRepository
                .Get(x => x.tokenHash == hash && x.purpose == purpose && x.usedAt == null && x.expiresAt > now)
                .FirstOrDefault();
        }

        private void MarkTokenUsed(AccountToken t)
        {
            t.usedAt = DateTime.Now;
            unitOfWork.AccountTokenRepository.Update(t);
            unitOfWork.Save();
        }

        private bool RateLimitOk(string email)
        {
            DateTime cutoff = DateTime.Now.AddHours(-1);
            int recent = unitOfWork.AccountTokenRepository
                .Get(x => x.email.ToLower() == email.ToLower() && x.createdDateTime >= cutoff)
                .Count();
            return recent < MaxTokensPerEmailPerHour;
        }

        // Συμβατό με τις απαιτήσεις του SqlMembershipProvider (defaults:
        // minRequiredPasswordLength=7, minRequiredNonalphanumericCharacters=1)
        private static string ValidatePassword(string password)
        {
            password = password ?? "";
            if (password.Length < Membership.MinRequiredPasswordLength)
                return "Ο κωδικός πρέπει να έχει τουλάχιστον " + Membership.MinRequiredPasswordLength + " χαρακτήρες.";
            if (password.Count(c => !char.IsLetterOrDigit(c)) < Membership.MinRequiredNonAlphanumericCharacters)
                return "Ο κωδικός πρέπει να περιέχει τουλάχιστον " + Membership.MinRequiredNonAlphanumericCharacters + " ειδικό χαρακτήρα (π.χ. ! @ # $).";
            return null;
        }

        private static string Sha256Hex(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(64);
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
