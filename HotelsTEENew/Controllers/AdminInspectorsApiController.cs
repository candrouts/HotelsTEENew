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
    public class AdminInspectorsApiController : ApiController
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

        // Λίστα επιθεωρητών με σύνοψη περιοχών δραστηριότητας
        [Route("api/AdminInspectorsApi/GetInspectors")]
        [HttpPost]
        public IHttpActionResult GetInspectors()
        {
            try
            {
                if (!IsAdmin())
                    return Ok(new { success = false });

                string sql = @"
                    SELECT i.id, i.firstName, i.lastName, i.email, i.phone, i.taxNumber,
                        STUFF((
                            SELECT ', ' + ea2.title
                            FROM TEE_Inspector_Areas ia2
                            INNER JOIN ELSTATAreas ea2 ON ea2.kalID = ia2.kalID
                            WHERE ia2.inspectorID = i.id
                            ORDER BY ea2.levelID, ea2.title
                            FOR XML PATH(''), TYPE
                        ).value('.','NVARCHAR(MAX)'), 1, 2, '') AS areas
                    FROM TEE_Inspectors i
                    WHERE i.isActive = 1
                    ORDER BY i.lastName, i.firstName";

                List<InspectorSearchViewModel> inspectors = unitOfWork.context.Database
                    .SqlQuery<InspectorSearchViewModel>(sql)
                    .ToList();

                return Ok(new { success = true, inspectors = inspectors });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminInspectorsApiController.cs");
                return Ok(new { success = false });
            }
        }

        // Δέντρο περιοχών (Περιφέρειες/ΠΕ) με τις επιλογές του συγκεκριμένου επιθεωρητή
        [Route("api/AdminInspectorsApi/GetInspectorAreas")]
        [HttpPost]
        public IHttpActionResult GetInspectorAreas([FromBody] InspectorSubmitViewModel model)
        {
            try
            {
                if (!IsAdmin())
                    return Ok(new { success = false });

                if (model == null || model.inspectorID == 0)
                    return Ok(new { success = false });

                string sql = "SELECT kalID, title, levelID, parentID FROM ELSTATAreas WHERE levelID=3 AND isActive=1 ORDER BY title";
                List<ELSTATAreaViewModel> perifereies = unitOfWork.context.Database.SqlQuery<ELSTATAreaViewModel>(sql).ToList();

                sql = "SELECT kalID, title, levelID, parentID FROM ELSTATAreas WHERE levelID=4 AND isActive=1 ORDER BY title";
                List<ELSTATAreaViewModel> allPes = unitOfWork.context.Database.SqlQuery<ELSTATAreaViewModel>(sql).ToList();

                List<InspectorArea> savedAreas = unitOfWork.InspectorAreaRepository
                    .Get(x => x.inspectorID == model.inspectorID).ToList();

                var result = new List<object>();
                foreach (var per in perifereies)
                {
                    bool coversAll = savedAreas.Any(x => x.kalID == per.kalID && x.levelID == 3);
                    var pes = allPes.Where(p => p.parentID == per.kalID).ToList();

                    result.Add(new
                    {
                        kalID = per.kalID,
                        title = per.title,
                        coversAll = coversAll,
                        pes = pes.Select(pe => new
                        {
                            kalID = pe.kalID,
                            title = pe.title,
                            isChecked = coversAll || savedAreas.Any(x => x.kalID == pe.kalID && x.levelID == 4)
                        }).ToList()
                    });
                }

                return Ok(new { success = true, perifereies = result });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminInspectorsApiController.cs");
                return Ok(new { success = false });
            }
        }

        // Αποθήκευση περιοχών δραστηριότητας επιθεωρητή (από τον admin)
        [Route("api/AdminInspectorsApi/SaveInspectorAreas")]
        [HttpPost]
        public IHttpActionResult SaveInspectorAreas([FromBody] AdminInspectorAreasSaveViewModel model)
        {
            try
            {
                if (!IsAdmin())
                    return Ok(new ApiAnswer { success = false });

                if (model == null || model.inspectorID == 0)
                    return Ok(new ApiAnswer { success = false });

                // Διαγραφή παλαιών
                List<InspectorArea> existing = unitOfWork.InspectorAreaRepository
                    .Get(x => x.inspectorID == model.inspectorID).ToList();
                foreach (var old in existing)
                    unitOfWork.InspectorAreaRepository.Delete(old);

                // Εισαγωγή νέων
                if (model.areas != null)
                {
                    foreach (var area in model.areas)
                    {
                        unitOfWork.InspectorAreaRepository.Insert(new InspectorArea
                        {
                            inspectorID = model.inspectorID,
                            kalID = area.kalID,
                            levelID = area.levelID
                        });
                    }
                }

                unitOfWork.Save();
                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminInspectorsApiController.cs");
                return Ok(new ApiAnswer { success = false });
            }
        }
    }
}
