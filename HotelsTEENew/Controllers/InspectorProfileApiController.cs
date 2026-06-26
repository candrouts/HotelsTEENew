using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class InspectorProfileApiController : ApiController
    {
        UnitOfWork unitOfWork = new UnitOfWork();

        // Βοηθητική: βρίσκει τον inspectorID του logged-in χρήστη
        private decimal? GetInspectorID()
        {
            string sql = "SELECT * FROM V_TEE_Users WHERE UserName = @UserName";
            UserViewModel user = unitOfWork.context.Database
                .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                .FirstOrDefault();
            return user?.tee_inspectorID;
        }

        [Route("api/InspectorProfileApi/GetProfile")]
        [HttpPost]
        public IHttpActionResult GetProfile()
        {
            try
            {
                decimal? inspectorID = GetInspectorID();
                if (inspectorID == null)
                    return Ok(new { success = false, message = "Inspector not found" });

                // Φόρτωση όλων των Περιφερειών
                string sql = "SELECT kalID, title, levelID, parentID FROM ELSTATAreas WHERE levelID=3 AND isActive=1 ORDER BY title";
                List<ELSTATAreaViewModel> perifereies = unitOfWork.context.Database.SqlQuery<ELSTATAreaViewModel>(sql).ToList();

                // Φόρτωση όλων των ΠΕ
                sql = "SELECT kalID, title, levelID, parentID FROM ELSTATAreas WHERE levelID=4 AND isActive=1 ORDER BY title";
                List<ELSTATAreaViewModel> allPes = unitOfWork.context.Database.SqlQuery<ELSTATAreaViewModel>(sql).ToList();

                // Φόρτωση αποθηκευμένων περιοχών του επιθεωρητή
                List<InspectorArea> savedAreas = unitOfWork.InspectorAreaRepository
                    .Get(x => x.inspectorID == inspectorID.Value).ToList();

                var result = new List<object>();

                foreach (var per in perifereies)
                {
                    bool coversAll = savedAreas.Any(x => x.kalID == per.kalID && x.levelID == 3);
                    var pes = allPes.Where(p => p.parentID == per.kalID).ToList();

                    var peList = pes.Select(pe => new
                    {
                        kalID = pe.kalID,
                        title = pe.title,
                        isChecked = coversAll || savedAreas.Any(x => x.kalID == pe.kalID && x.levelID == 4)
                    }).ToList();

                    result.Add(new
                    {
                        kalID = per.kalID,
                        title = per.title,
                        coversAll = coversAll,
                        pes = peList
                    });
                }

                return Ok(new { success = true, perifereies = result });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }

        [Route("api/InspectorProfileApi/SaveAreas")]
        [HttpPost]
        public IHttpActionResult SaveAreas([FromBody] List<InspectorAreaSaveViewModel> areas)
        {
            try
            {
                decimal? inspectorID = GetInspectorID();
                if (inspectorID == null)
                    return Ok(new ApiAnswer { success = false });

                // Διαγραφή παλαιών εγγραφών
                List<InspectorArea> existing = unitOfWork.InspectorAreaRepository
                    .Get(x => x.inspectorID == inspectorID.Value).ToList();
                foreach (var old in existing)
                    unitOfWork.InspectorAreaRepository.Delete(old);

                // Εισαγωγή νέων
                if (areas != null)
                {
                    foreach (var area in areas)
                    {
                        unitOfWork.InspectorAreaRepository.Insert(new InspectorArea
                        {
                            inspectorID = inspectorID.Value,
                            kalID = area.kalID,
                            levelID = area.levelID
                        });
                    }
                }

                unitOfWork.Save();
                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception)
            {
                return Ok(new ApiAnswer { success = false });
            }
        }
    }
}
