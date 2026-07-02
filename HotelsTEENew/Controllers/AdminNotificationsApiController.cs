using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.Utils;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class AdminNotificationsApiController : ApiController
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

        // Λίστα γεγονότων (catalog) + templates (όσα έχουν ρυθμιστεί)
        [Route("api/AdminNotificationsApi/GetEvents")]
        [HttpPost]
        public IHttpActionResult GetEvents()
        {
            if (!IsAdmin())
                return Ok(new { success = false });

            List<NotificationTemplate> templates = unitOfWork.NotificationTemplateRepository.Get().ToList();

            var rows = NotificationEvents.All.Select(ev =>
            {
                NotificationTemplate t = templates.FirstOrDefault(x => x.eventKey == ev.key);
                return new NotificationEventRowViewModel
                {
                    eventKey = ev.key,
                    title = ev.title,
                    description = ev.description,
                    tokens = ev.tokens.ToList(),
                    // Αν δεν υπάρχει ρυθμισμένο template, δείχνουμε τα defaults του catalog
                    isActive = t != null ? t.isActive : false,
                    recipientType = t != null ? t.recipientType : ev.defaultRecipient,
                    customEmail = t != null ? t.customEmail : null,
                    subject = t != null ? t.subject : ev.defaultSubject,
                    body = t != null ? t.body : ev.defaultBody
                };
            }).ToList();

            return Ok(new { success = true, events = rows });
        }

        // Αποθήκευση template ενός γεγονότος (upsert ανά eventKey)
        [Route("api/AdminNotificationsApi/SaveTemplate")]
        [HttpPost]
        public IHttpActionResult SaveTemplate([FromBody] NotificationTemplateSaveViewModel model)
        {
            if (!IsAdmin())
                return Ok(new ApiAnswer { success = false });

            if (model == null || string.IsNullOrEmpty(model.eventKey) || NotificationEvents.Get(model.eventKey) == null)
                return Ok(new ApiAnswer { success = false });

            // Custom recipient απαιτεί email
            if (model.recipientType == 4 && string.IsNullOrWhiteSpace(model.customEmail))
                return Ok(new ApiAnswer { success = false });

            try
            {
                NotificationTemplate t = unitOfWork.NotificationTemplateRepository
                    .Get(x => x.eventKey == model.eventKey).FirstOrDefault();

                if (t == null)
                {
                    t = new NotificationTemplate { eventKey = model.eventKey };
                    t.isActive = model.isActive;
                    t.recipientType = model.recipientType;
                    t.customEmail = model.customEmail;
                    t.subject = model.subject ?? "";
                    t.body = model.body ?? "";
                    unitOfWork.NotificationTemplateRepository.Insert(t);
                }
                else
                {
                    t.isActive = model.isActive;
                    t.recipientType = model.recipientType;
                    t.customEmail = model.customEmail;
                    t.subject = model.subject ?? "";
                    t.body = model.body ?? "";
                    unitOfWork.NotificationTemplateRepository.Update(t);
                }

                unitOfWork.Save();
                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminNotificationsApiController.cs");
                return Ok(new ApiAnswer { success = false });
            }
        }

        // Ιστορικό αποστολών (πιο πρόσφατα πρώτα)
        [Route("api/AdminNotificationsApi/GetLog")]
        [HttpPost]
        public IHttpActionResult GetLog()
        {
            if (!IsAdmin())
                return Ok(new { success = false });

            string sql = "SELECT TOP 200 id, eventKey, toEmail, subject, sentDateTime, success, error FROM TEE_NotificationLog ORDER BY sentDateTime DESC";
            List<NotificationLogRowViewModel> log = unitOfWork.context.Database
                .SqlQuery<NotificationLogRowViewModel>(sql).ToList();

            return Ok(new { success = true, log = log });
        }
    }
}
