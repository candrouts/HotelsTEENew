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
    public class MenuApiController : ApiController
    {

        UnitOfWork unitOfWork = new UnitOfWork();

        [Route("api/MenuApi/GetMenu")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetMenu()
        {
           
            try
            {
               
              
                string sql = "Select * from V_TEE_Users where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                if (user != null && (user.role == 1 || user.role == 10 || user.role == 100))
                {

                    return Ok(user);
                }
                else
                {
                    return null;
                }
                 


            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "MenuApiController.cs");
                //Utils.Mailer.SendEmailException(e);
                //Utils.ExceptionHandler.ToDetailedString(e);
                return Ok(new ApiAnswer { success = false, responseText = "Your message successfuly sent!" });
            }

        }

    }
}
