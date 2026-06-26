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
    public class AdminMedalsApiController : ApiController
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

        // Μήτρα: μετάλια × κύριοι πυλώνες + οι βάσεις + ο διακόπτης
        [Route("api/AdminMedalsApi/GetMatrix")]
        [HttpPost]
        public IHttpActionResult GetMatrix()
        {
            if (!IsAdmin())
                return Ok(new { success = false });

            List<MedalRowViewModel> medals = unitOfWork.MedalRepository.Get()
                .OrderBy(m => m.min)
                .Select(m => new MedalRowViewModel { id = m.id, title = m.title, min = m.min, max = m.max })
                .ToList();

            // Κύριοι πυλώνες (parentID IS NULL)
            string sql = "Select * from V_TEE_Categories where isActive=1 and parentID is null order by [order]";
            List<PillarRowViewModel> pillars = unitOfWork.context.Database
                .SqlQuery<CategoryViewModel>(sql).ToList()
                .Select(c => new PillarRowViewModel { id = c.id, title = c.title, totalUnits = c.totalUnits ?? 0 })
                .ToList();

            List<ThresholdCellViewModel> thresholds = unitOfWork.MedalPillarThresholdRepository.Get()
                .Select(t => new ThresholdCellViewModel
                {
                    medalID = t.medalID,
                    categoryID = t.categoryID,
                    minValue = t.minValue,
                    isPercent = t.isPercent
                }).ToList();

            bool usePillar = Utils.ScoringHelper.GetBoolSetting(unitOfWork, "medal.usePillarThresholds", false);

            return Ok(new { success = true, usePillarThresholds = usePillar, medals, pillars, thresholds });
        }

        // Αποθήκευση ορίων συνολικής βαθμολογίας ανά μετάλιο (TEE_Medals)
        [Route("api/AdminMedalsApi/SaveMedals")]
        [HttpPost]
        public IHttpActionResult SaveMedals([FromBody] List<MedalRowViewModel> medals)
        {
            if (!IsAdmin())
                return Ok(new ApiAnswer { success = false });
            if (medals == null)
                return Ok(new ApiAnswer { success = false });

            try
            {
                foreach (var m in medals)
                {
                    Medal med = unitOfWork.MedalRepository.GetByID(m.id);
                    if (med == null) continue;
                    med.title = m.title;
                    med.min = m.min;
                    med.max = m.max;
                    med.points = (int)decimal.Round(m.min);   // points = κατώφλι, για συνέπεια
                    unitOfWork.MedalRepository.Update(med);
                }
                unitOfWork.Save();
                return Ok(new ApiAnswer { success = true });
            }
            catch (Exception)
            {
                return Ok(new ApiAnswer { success = false });
            }
        }

        // Αποθήκευση: αντικατάσταση όλων των βάσεων + ο διακόπτης
        [Route("api/AdminMedalsApi/SaveMatrix")]
        [HttpPost]
        public IHttpActionResult SaveMatrix([FromBody] MedalMatrixSaveViewModel model)
        {
            if (!IsAdmin())
                return Ok(new ApiAnswer { success = false });
            if (model == null)
                return Ok(new ApiAnswer { success = false });

            try
            {
                // Διακόπτης
                AppSetting s = unitOfWork.SettingRepository.Get(x => x.settingKey == "medal.usePillarThresholds").FirstOrDefault();
                if (s == null)
                {
                    s = new AppSetting { settingKey = "medal.usePillarThresholds", settingValue = model.usePillarThresholds ? "1" : "0" };
                    unitOfWork.SettingRepository.Insert(s);
                }
                else
                {
                    s.settingValue = model.usePillarThresholds ? "1" : "0";
                    unitOfWork.SettingRepository.Update(s);
                }

                // Αντικατάσταση όλων των βάσεων
                List<MedalPillarThreshold> existing = unitOfWork.MedalPillarThresholdRepository.Get().ToList();
                foreach (var old in existing)
                    unitOfWork.MedalPillarThresholdRepository.Delete(old);

                if (model.cells != null)
                {
                    foreach (var c in model.cells)
                    {
                        // Κρατάμε μόνο κελιά με θετική βάση
                        if (c.minValue <= 0) continue;
                        unitOfWork.MedalPillarThresholdRepository.Insert(new MedalPillarThreshold
                        {
                            medalID = c.medalID,
                            categoryID = c.categoryID,
                            minValue = c.minValue,
                            isPercent = c.isPercent
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
