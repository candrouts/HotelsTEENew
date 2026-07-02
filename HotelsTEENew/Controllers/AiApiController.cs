using HotelsTEE.DAL;
using HotelsTEE.ViewModels;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    // AI endpoints (ai branch / greencertai) — όλα πίσω από ai.enabled.
    [Authorize]
    public class AiApiController : ApiController
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

        // Διαγνωστικό: επαλήθευση σύνδεσης με Azure OpenAI (admin only).
        [Route("api/AiApi/Ping")]
        [HttpPost]
        public IHttpActionResult Ping()
        {
            try
            {
                if (!IsAdmin())
                    return Ok(new { success = false, message = "Δεν επιτρέπεται." });

                if (!Utils.AiService.IsEnabled())
                    return Ok(new { success = false, message = "Το AI είναι απενεργοποιημένο (ai.enabled=0 ή λείπουν endpoints/keys)." });

                string reply = Utils.AiService.Chat(
                    "Είσαι ο βοηθός του Συστήματος Περιβαλλοντικής Κατάταξης Τουριστικών Καταλυμάτων.",
                    "Απάντησε ακριβώς με τη φράση: Η σύνδεση με το Azure OpenAI λειτουργεί.",
                    0m, 50);

                if (string.IsNullOrEmpty(reply))
                    return Ok(new { success = false, message = "Καμία απάντηση — δείτε το TEE_ErrorLog για το σφάλμα HTTP." });

                return Ok(new { success = true, message = reply });
            }
            catch (Exception ex)
            {
                Utils.ErrorLogger.Log(ex, "AiApiController.Ping");
                return Ok(new { success = false, message = "Σφάλμα — δείτε το TEE_ErrorLog." });
            }
        }
    }
}
