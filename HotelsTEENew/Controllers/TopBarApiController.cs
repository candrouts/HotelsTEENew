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
    public class TopBarApiController : ApiController
    {

        UnitOfWork unitOfWork = new UnitOfWork();

        [Route("api/TopBarApi/GetHotelDetails/{id:decimal}")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetHotelDetails(decimal id)
        {

            try
            {
                HotelDetailsViewModel hotelDetails = null;


                string sql = @"Select * from V_TEE_Users where UserName =@UserName";
                UserViewModel user = unitOfWork.context.Database.SqlQuery<UserViewModel>(sql,
                        new SqlParameter("@UserName", User.Identity.Name))
                        .FirstOrDefault();

                if (user != null && user.role == 1 )
                {
                    string sqlHD = @"Select * from V_TEE_HotelDetails where UserName =  @UserName";
                     hotelDetails = unitOfWork.context.Database.SqlQuery<HotelDetailsViewModel>(sqlHD,
                        new SqlParameter("@UserName", User.Identity.Name))
                        .FirstOrDefault();

                }
                else if (user != null &&  user.role == 10)
                {


                    string certSql = @"SELECT  hotelID, exploitingCompanyID FROM V_TEE_Certificates_Inspector WHERE certificateID = @certificateID and UserName =  @UserName";
                    CertificateViewModel certificate = unitOfWork.context.Database.SqlQuery<CertificateViewModel>(certSql,
                        new SqlParameter("@certificateID", id),
                        new SqlParameter("@UserName", User.Identity.Name))
                        .FirstOrDefault();




                    string sqlHD = @"SELECT * FROM V_TEE_HotelDetails WHERE hotelID = @hotelID AND exploitingCompanyID = @exploitingCompanyID";

                     hotelDetails = unitOfWork.context.Database.SqlQuery<HotelDetailsViewModel>(sqlHD,
                            new SqlParameter("@hotelID", certificate.hotelID),
                            new SqlParameter("@exploitingCompanyID", certificate.exploitingCompanyID))
                    .FirstOrDefault();
                }
                else
                {
                    return Unauthorized();
                }

                if (hotelDetails == null)
                    return NotFound();

                return Ok(hotelDetails);
            }
            catch (System.Exception ex)
            {
                return InternalServerError(ex);
            }

        }

    }
}
