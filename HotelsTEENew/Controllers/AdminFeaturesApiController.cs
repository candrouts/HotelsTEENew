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
    // Διαχείριση κεντρικών ρυθμίσεων/παροχών & αντιστοιχίσεων με κριτήρια (admin).
    [Authorize]
    public class AdminFeaturesApiController : ApiController
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

        [Route("api/AdminFeaturesApi/GetData")]
        [HttpPost]
        public IHttpActionResult GetData()
        {
            var result = new AdminFeaturesDataViewModel
            {
                success = false,
                features = new List<FeatureItemViewModel>(),
                criteria = new List<EligibleCriteriaViewModel>()
            };

            try
            {
                if (!IsAdmin()) return Ok(result);

                // Επιλέξιμα κριτήρια: μόνο όσα έχουν notApplicable=1
                string sql = @"SELECT id, code, title, categoryTitle
                               FROM V_TEE_Criteria
                               WHERE notApplicable = 1
                                 AND dateFrom <= getdate() AND dateTo >= getdate()
                               ORDER BY categoryTitle, [order]";
                result.criteria = unitOfWork.context.Database
                    .SqlQuery<EligibleCriteriaViewModel>(sql).ToList();

                var critById = result.criteria.ToDictionary(c => c.id);

                List<PropertyFeature> features = unitOfWork.PropertyFeatureRepository
                    .Get().OrderBy(x => x.displayOrder).ThenBy(x => x.featureID).ToList();

                List<FeatureCriteriaMap> maps = unitOfWork.FeatureCriteriaMapRepository.Get().ToList();

                foreach (var f in features)
                {
                    var item = new FeatureItemViewModel
                    {
                        featureID = f.featureID,
                        title = f.title,
                        description = f.description,
                        icon = f.icon,
                        displayOrder = f.displayOrder,
                        isActive = f.isActive,
                        mappings = new List<FeatureMapItemViewModel>()
                    };

                    foreach (var m in maps.Where(x => x.featureID == f.featureID))
                    {
                        EligibleCriteriaViewModel c;
                        critById.TryGetValue(m.criteriaID, out c);
                        item.mappings.Add(new FeatureMapItemViewModel
                        {
                            mapID = m.mapID,
                            criteriaID = m.criteriaID,
                            criteriaCode = c != null ? c.code : ("#" + m.criteriaID),
                            criteriaTitle = c != null ? c.title : "(κριτήριο εκτός λίστας)",
                            disableWhenPresent = m.disableWhenPresent
                        });
                    }

                    result.features.Add(item);
                }

                result.success = true;
                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminFeaturesApiController.cs");
                return Ok(result);
            }
        }

        [Route("api/AdminFeaturesApi/SaveFeature")]
        [HttpPost]
        public IHttpActionResult SaveFeature([FromBody] FeatureSaveRequest req)
        {
            var res = new FeatureApiResult { success = false };
            try
            {
                if (!IsAdmin()) { res.message = "Δεν επιτρέπεται."; return Ok(res); }
                if (req == null || string.IsNullOrWhiteSpace(req.title))
                {
                    res.message = "Συμπληρώστε τίτλο.";
                    return Ok(res);
                }

                PropertyFeature f;
                if (req.featureID.HasValue && req.featureID.Value > 0)
                {
                    f = unitOfWork.PropertyFeatureRepository.GetByID(req.featureID.Value);
                    if (f == null) { res.message = "Δεν βρέθηκε."; return Ok(res); }
                    f.title = req.title.Trim();
                    f.description = req.description;
                    f.icon = req.icon;
                    f.displayOrder = req.displayOrder;
                    f.isActive = req.isActive;
                    unitOfWork.PropertyFeatureRepository.Update(f);
                }
                else
                {
                    f = new PropertyFeature
                    {
                        title = req.title.Trim(),
                        description = req.description,
                        icon = req.icon,
                        displayOrder = req.displayOrder,
                        isActive = req.isActive
                    };
                    unitOfWork.PropertyFeatureRepository.Insert(f);
                }

                unitOfWork.Save();
                res.success = true;
                res.id = f.featureID;
                return Ok(res);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminFeaturesApiController.cs");
                res.message = "Σφάλμα αποθήκευσης.";
                return Ok(res);
            }
        }

        [Route("api/AdminFeaturesApi/DeleteFeature")]
        [HttpPost]
        public IHttpActionResult DeleteFeature([FromBody] FeatureSaveRequest req)
        {
            var res = new FeatureApiResult { success = false };
            try
            {
                if (!IsAdmin()) return Ok(res);
                if (req == null || !req.featureID.HasValue) return Ok(res);

                PropertyFeature f = unitOfWork.PropertyFeatureRepository.GetByID(req.featureID.Value);
                if (f == null) { res.success = true; return Ok(res); }

                // Διαγραφή και των αντιστοιχίσεων του feature
                List<FeatureCriteriaMap> maps = unitOfWork.FeatureCriteriaMapRepository
                    .Get(x => x.featureID == req.featureID.Value).ToList();
                foreach (var m in maps)
                    unitOfWork.FeatureCriteriaMapRepository.Delete(m);

                unitOfWork.PropertyFeatureRepository.Delete(f);
                unitOfWork.Save();
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminFeaturesApiController.cs");
                return Ok(res);
            }
        }

        [Route("api/AdminFeaturesApi/SaveMapping")]
        [HttpPost]
        public IHttpActionResult SaveMapping([FromBody] MappingSaveRequest req)
        {
            var res = new FeatureApiResult { success = false };
            try
            {
                if (!IsAdmin()) return Ok(res);
                if (req == null || req.featureID <= 0 || req.criteriaID <= 0)
                {
                    res.message = "Επιλέξτε κριτήριο.";
                    return Ok(res);
                }

                FeatureCriteriaMap m;
                if (req.mapID.HasValue && req.mapID.Value > 0)
                {
                    m = unitOfWork.FeatureCriteriaMapRepository.GetByID(req.mapID.Value);
                    if (m == null) { res.message = "Δεν βρέθηκε."; return Ok(res); }
                    m.criteriaID = req.criteriaID;
                    m.disableWhenPresent = req.disableWhenPresent;
                    unitOfWork.FeatureCriteriaMapRepository.Update(m);
                }
                else
                {
                    // Αποφυγή διπλοεγγραφής ίδιου κριτηρίου στο ίδιο feature
                    bool exists = unitOfWork.FeatureCriteriaMapRepository
                        .Get(x => x.featureID == req.featureID && x.criteriaID == req.criteriaID).Any();
                    if (exists) { res.message = "Το κριτήριο υπάρχει ήδη σε αυτή τη ρύθμιση."; return Ok(res); }

                    m = new FeatureCriteriaMap
                    {
                        featureID = req.featureID,
                        criteriaID = req.criteriaID,
                        disableWhenPresent = req.disableWhenPresent
                    };
                    unitOfWork.FeatureCriteriaMapRepository.Insert(m);
                }

                unitOfWork.Save();
                res.success = true;
                res.id = m.mapID;
                return Ok(res);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminFeaturesApiController.cs");
                res.message = "Σφάλμα αποθήκευσης.";
                return Ok(res);
            }
        }

        [Route("api/AdminFeaturesApi/DeleteMapping")]
        [HttpPost]
        public IHttpActionResult DeleteMapping([FromBody] MappingSaveRequest req)
        {
            var res = new FeatureApiResult { success = false };
            try
            {
                if (!IsAdmin()) return Ok(res);
                if (req == null || !req.mapID.HasValue) return Ok(res);

                FeatureCriteriaMap m = unitOfWork.FeatureCriteriaMapRepository.GetByID(req.mapID.Value);
                if (m != null)
                {
                    unitOfWork.FeatureCriteriaMapRepository.Delete(m);
                    unitOfWork.Save();
                }
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminFeaturesApiController.cs");
                return Ok(res);
            }
        }
    }
}
