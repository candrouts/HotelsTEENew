using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.Utils;
using HotelsTEE.ViewModels;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class AdminCertTemplateApiController : ApiController
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

        [Route("api/AdminCertTemplateApi/GetTemplate")]
        [HttpPost]
        public IHttpActionResult GetTemplate()
        {
            if (!IsAdmin())
                return Ok(new { success = false });

            CertificateTemplate t = unitOfWork.CertificateTemplateRepository
                .Get().OrderByDescending(x => x.id).FirstOrDefault();

            string body = (t != null && !string.IsNullOrWhiteSpace(t.body))
                ? t.body : CertificateDocService.DefaultTemplate();

            return Ok(new
            {
                success = true,
                body = body,
                tokens = CertificateDocService.Tokens
            });
        }

        [Route("api/AdminCertTemplateApi/SaveTemplate")]
        [HttpPost]
        public IHttpActionResult SaveTemplate([FromBody] NotificationTemplateSaveViewModel model)
        {
            // (επαναχρησιμοποιούμε το body πεδίο)
            if (!IsAdmin())
                return Ok(new ApiAnswer { success = false });
            if (model == null)
                return Ok(new ApiAnswer { success = false });

            try
            {
                CertificateTemplate t = unitOfWork.CertificateTemplateRepository
                    .Get().OrderByDescending(x => x.id).FirstOrDefault();

                if (t == null)
                {
                    t = new CertificateTemplate
                    {
                        title = "Βεβαίωση Βιωσιμότητας",
                        body = model.body ?? "",
                        isActive = true,
                        lastModified = DateTime.Now
                    };
                    unitOfWork.CertificateTemplateRepository.Insert(t);
                }
                else
                {
                    t.body = model.body ?? "";
                    t.isActive = true;
                    t.lastModified = DateTime.Now;
                    unitOfWork.CertificateTemplateRepository.Update(t);
                }

                unitOfWork.Save();
                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCertTemplateApiController.cs");
                return Ok(new ApiAnswer { success = false });
            }
        }
    }
}
