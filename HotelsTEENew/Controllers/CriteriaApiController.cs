using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing.Printing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace HotelsTEE.Controllers
{
    [Authorize]
    public class CriteriaApiController : ApiController
    {

        UnitOfWork unitOfWork = new UnitOfWork();

        [Route("api/CriteriaApi/GetAllCriteria")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetAllCriteria(bool create = false)
        {
            Results results = new Results();

            try
            {
                string sql = "Select * from V_TEE_Categories where isActive=1 order by [order] ";
                List<CategoryViewModel> allCategories = unitOfWork.context.Database.SqlQuery<CategoryViewModel>(sql).ToList();

                sql = "Select * from V_TEE_Criteria where dateFrom <= getdate() AND dateTo >= getDate() order by [order] ";
                List<CriteriaViewModel> allCriteria = unitOfWork.context.Database.SqlQuery<CriteriaViewModel>(sql).ToList();

                sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
                HotelDetailsViewModel hotelDetails = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                List<CategoryViewModel> categories = allCategories.Where(x => !x.parentID.HasValue).OrderBy(x=>x.order).ToList();

                sql = "Select * from V_TEE_Criteria_Files where isActive=1 ";
                List<CriteriaFileViewModel> allCriteriaFiles = unitOfWork.context.Database.SqlQuery<CriteriaFileViewModel>(sql).ToList();

                sql = "Select * from V_TEE_HotelCriteria where hotelID = @hotelID AND exploitingCompanyID = @companyID and version=1 and isFinished=0";
                HotelCriteriaViewModel hotelCriteria = unitOfWork.context.Database
                    .SqlQuery<HotelCriteriaViewModel>(sql,
                        new SqlParameter("@hotelID", hotelDetails.hotelID),
                        new SqlParameter("@companyID", hotelDetails.exploitingCompanyID))
                    .FirstOrDefault();

                // Δεν υπάρχει ενεργή αυτοαξιολόγηση: ΜΗΝ δημιουργείς αυτόματα.
                // Επιστρέφουμε flag ώστε η σελίδα να ζητήσει επιβεβαίωση από τον χρήστη.
                if (hotelCriteria == null && !create)
                {
                    results.needsNewAssessment = true;
                    results.hotelDetails = hotelDetails;
                    results.categories = new List<CategoryViewModel>();
                    results.medals = new List<MedalViewModel>();
                    return Ok(results);
                }

                if (hotelCriteria == null)
                {
                    try
                    {
                        // Νέος κύκλος: αν υπάρχει ολοκληρωμένη τελική κατάταξη (v3),
                        // η νέα v1 προσυμπληρώνεται από αυτήν (κριτήρια + τεκμήρια) —
                        // ίδια συμπεριφορά με το κουμπί "Έναρξη Νέας Αξιολόγησης".
                        HotelCriteria sourceV3 = unitOfWork.HotelCriteriaRepository
                            .Get(x => x.hotelID == hotelDetails.hotelID
                                   && x.exploitingCompanyID == hotelDetails.exploitingCompanyID
                                   && x.version == 3 && x.status == 2 && x.isFinished == true)
                            .OrderByDescending(x => x.id)
                            .FirstOrDefault();

                        HotelCriteria hotCrit = new HotelCriteria();

                        hotCrit.status = 1;
                        hotCrit.version = 1;
                        hotCrit.exploitingCompanyID = hotelDetails.exploitingCompanyID;
                        hotCrit.hotelID = hotelDetails.hotelID;
                        hotCrit.isFinished = false;
                        hotCrit.creationDatetime = DateTime.Now;
                        if (sourceV3 != null)
                        {
                            hotCrit.maxPoints = sourceV3.maxPoints;
                            hotCrit.totalPoints = sourceV3.totalPoints;
                            hotCrit.medalID = sourceV3.medalID;
                        }

                        unitOfWork.HotelCriteriaRepository.Insert(hotCrit);

                        // Προσυμπλήρωση απαντήσεων κριτηρίων από την v3
                        if (sourceV3 != null)
                        {
                            List<HotelCriteria_Criteria> oldCriteria = unitOfWork.HotelCriteria_CriteriaRepository
                                .Get(x => x.hotelCriteriaID == sourceV3.id).ToList();
                            foreach (var z in oldCriteria)
                            {
                                unitOfWork.HotelCriteria_CriteriaRepository.Insert(new HotelCriteria_Criteria
                                {
                                    criteriaID = z.criteriaID,
                                    hotelCriteria = hotCrit,
                                    isApplicable = z.isApplicable,
                                    isChecked = z.isChecked,
                                    isNotChecked = z.isNotChecked,
                                    points = z.points,
                                    value = z.value
                                });
                            }
                        }

                        unitOfWork.Save();

                        // Προσυμπλήρωση τεκμηρίων (metadata + Azure) από την v3
                        if (sourceV3 != null)
                        {
                            try
                            {
                                List<HotelCriteria_CriteriaFile> oldFiles = unitOfWork.HotelCriteria_CriteriaFileRepository
                                    .Get(x => x.hotelCriteriaID == sourceV3.id).ToList();

                                foreach (var f in oldFiles)
                                {
                                    unitOfWork.HotelCriteria_CriteriaFileRepository.Insert(new HotelCriteria_CriteriaFile
                                    {
                                        hotelCriteriaID = hotCrit.id,
                                        criteriaFileID = f.criteriaFileID,
                                        fileName = f.fileName,
                                        fileType = f.fileType,
                                        creationDateTime = DateTime.Now
                                    });

                                    AzureStorage.AzureStorage.CopyFileBetweenCriteria(
                                        sourceV3.id.ToString(), hotCrit.id.ToString(),
                                        f.criteriaFileID.ToString(), f.fileName);
                                }

                                if (oldFiles.Count > 0)
                                    unitOfWork.Save();
                            }
                            catch (Exception)
                            {
                                // Τα τεκμήρια δεν μπλοκάρουν τη δημιουργία του νέου κύκλου
                            }
                        }

                        // Προσυμπλήρωση κεντρικών ρυθμίσεων/παροχών από τον προηγούμενο κύκλο.
                        // Προτεραιότητα στην ΥΨΗΛΟΤΕΡΗ έκδοση (v3/v2 = ό,τι επιβεβαίωσε ο
                        // επιθεωρητής στην αυτοψία), με fallback στη δήλωση του ξενοδόχου (v1).
                        if (sourceV3 != null && sourceV3.certificateID.HasValue)
                        {
                            try
                            {
                                decimal prevCertID = sourceV3.certificateID.Value;
                                List<HotelCriteria> prevVersions = unitOfWork.HotelCriteriaRepository
                                    .Get(x => x.certificateID == prevCertID).ToList();
                                var verById = prevVersions.ToDictionary(v => v.id, v => v.version);
                                List<decimal> prevIds = prevVersions.Select(v => v.id).ToList();

                                List<HotelCriteriaFeature> prevFeats = unitOfWork.HotelCriteriaFeatureRepository
                                    .Get(x => prevIds.Contains(x.hotelCriteriaID)).ToList();

                                var best = prevFeats
                                    .GroupBy(x => x.featureID)
                                    .Select(g => g.OrderByDescending(x => verById.ContainsKey(x.hotelCriteriaID) ? verById[x.hotelCriteriaID] : 0).First());

                                foreach (var fa in best)
                                {
                                    unitOfWork.HotelCriteriaFeatureRepository.Insert(new HotelCriteriaFeature
                                    {
                                        hotelCriteriaID = hotCrit.id,
                                        featureID = fa.featureID,
                                        hasFeature = fa.hasFeature
                                    });
                                }
                                if (best.Any())
                                    unitOfWork.Save();
                            }
                            catch (Exception)
                            {
                                // Η προσυμπλήρωση ρυθμίσεων δεν μπλοκάρει τη δημιουργία του κύκλου
                            }
                        }


                        sql = "Select * from V_TEE_HotelCriteria where hotelID = @hotelID AND exploitingCompanyID = @companyID and version=1 and isFinished=0";
                        hotelCriteria = unitOfWork.context.Database
                            .SqlQuery<HotelCriteriaViewModel>(sql,
                                new SqlParameter("@hotelID", hotelDetails.hotelID),
                                new SqlParameter("@companyID", hotelDetails.exploitingCompanyID))
                            .FirstOrDefault();

                    }
                    catch(Exception e)
                    {

                    }
                }


                List<HotelCriteria_CriteriaViewModel> hotelCriteria_Criteria = new List<HotelCriteria_CriteriaViewModel>();
                if (hotelCriteria != null)
                {
                    sql = "Select * from V_TEE_HotelCriteria_Criteria where hotelCriteriaID = @hotelCriteriaID";
                    hotelCriteria_Criteria = unitOfWork.context.Database
                        .SqlQuery<HotelCriteria_CriteriaViewModel>(sql, new SqlParameter("@hotelCriteriaID", hotelCriteria.id))
                        .ToList();
                }


                sql = "Select * from [V_TEE_HotelCriteria_CriteriaFiles] where hotelCriteriaID = @hotelCriteriaID";
                List<HotelCriteria_CriteriaFileViewModel> hotelCriteria_criteriaFiles = unitOfWork.context.Database
                    .SqlQuery<HotelCriteria_CriteriaFileViewModel>(sql, new SqlParameter("@hotelCriteriaID", hotelCriteria.id))
                    .ToList();



                foreach (var z in categories)
                {
                    z.categories = allCategories.Where(x => x.parentID == z.id).OrderBy(x => x.order).ToList();

                   
                    foreach (var x in z.categories)
                    {
                        x.criteria = allCriteria.Where(c => c.categoryID == x.id).OrderBy(c => c.order).ToList();

                        foreach (var v in x.criteria)
                        {
                            if (v.needsFiles == true)
                                v.files = allCriteriaFiles.Where(b => b.criteriaID == v.id).ToList();

                           

                            HotelCriteria_CriteriaViewModel crit = hotelCriteria_Criteria.Where(b => b.criteriaID == v.id).FirstOrDefault();
                            if (crit != null)
                            {
                                v.value = crit.value;
                                v.isApplicable = crit.isApplicable;
                                v.isChecked = crit.isChecked;
                                v.isNotChecked = crit.isNotChecked;
                                v.points = crit.points;

                                if (v.files != null && v.files.Count > 0)
                                {
                                    foreach (var f in v.files)
                                    {
                                        f.files = hotelCriteria_criteriaFiles.Where(b => b.criteriaFileID == f.id).ToList();
                                    }
                                }

                            }
                            else {
                                v.isApplicable = true;
                            }

                        }
                    }

                }


                sql = "Select * from V_TEE_Medals order by min asc";
                List<MedalViewModel> allMedals = unitOfWork.context.Database.SqlQuery<MedalViewModel>(sql).ToList();

                results.medals = allMedals;
                results.categories = categories;
                results.hotelDetails = hotelDetails;
                results.hotelCriteria = hotelCriteria;

                results.usePillarThresholds = Utils.ScoringHelper.GetBoolSetting(unitOfWork, "medal.usePillarThresholds", false);
                results.medalThresholds = unitOfWork.MedalPillarThresholdRepository.Get()
                    .Select(t => new ThresholdCellViewModel { medalID = t.medalID, categoryID = t.categoryID, minValue = t.minValue, isPercent = t.isPercent })
                    .ToList();

                // Κεντρικές ρυθμίσεις/παροχές (ενεργές) + αντιστοιχίσεις + απαντήσεις του κύκλου
                List<PropertyFeature> activeFeatures = unitOfWork.PropertyFeatureRepository
                    .Get(x => x.isActive).OrderBy(x => x.displayOrder).ThenBy(x => x.featureID).ToList();
                results.features = activeFeatures.Select(f => new PropertyFeatureClientViewModel
                {
                    featureID = f.featureID, title = f.title, description = f.description, icon = f.icon
                }).ToList();

                List<decimal> featureIds = activeFeatures.Select(f => f.featureID).ToList();
                results.featureMaps = unitOfWork.FeatureCriteriaMapRepository
                    .Get(x => featureIds.Contains(x.featureID))
                    .Select(m => new FeatureMapClientViewModel
                    {
                        featureID = m.featureID, criteriaID = m.criteriaID, disableWhenPresent = m.disableWhenPresent
                    }).ToList();

                results.featureAnswers = new List<FeatureAnswerViewModel>();
                if (hotelCriteria != null)
                {
                    decimal hcId = hotelCriteria.id;
                    results.featureAnswers = unitOfWork.HotelCriteriaFeatureRepository
                        .Get(x => x.hotelCriteriaID == hcId)
                        .Select(a => new FeatureAnswerViewModel { featureID = a.featureID, hasFeature = a.hasFeature })
                        .ToList();
                }

                return Ok(results);
            }
            catch (Exception e)
            {

                return Ok(results);
            }


        }

        // Αποθήκευση απάντησης κεντρικής ρύθμισης/παροχής για τον τρέχοντα κύκλο (v1).
        [Route("api/CriteriaApi/SaveFeatureAnswer")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult SaveFeatureAnswer([FromBody] FeatureAnswerSaveRequest req)
        {
            var ans = new ApiAnswer { success = false };
            try
            {
                if (req == null || req.hotelCriteriaID <= 0 || req.featureID <= 0)
                    return Ok(ans);

                // Έλεγχος ιδιοκτησίας: το HotelCriteria ανήκει στο κατάλυμα του χρήστη
                HotelCriteria hc = unitOfWork.HotelCriteriaRepository.GetByID(req.hotelCriteriaID);
                if (hc == null) return Ok(ans);

                string sql = "Select * from V_TEE_HotelDetails where UserName = @UserName";
                HotelDetailsViewModel hd = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (hd == null || hc.hotelID != hd.hotelID || hc.exploitingCompanyID != hd.exploitingCompanyID)
                    return Ok(ans);

                HotelCriteriaFeature existing = unitOfWork.HotelCriteriaFeatureRepository
                    .Get(x => x.hotelCriteriaID == req.hotelCriteriaID && x.featureID == req.featureID)
                    .FirstOrDefault();

                if (existing == null)
                {
                    unitOfWork.HotelCriteriaFeatureRepository.Insert(new HotelCriteriaFeature
                    {
                        hotelCriteriaID = req.hotelCriteriaID,
                        featureID = req.featureID,
                        hasFeature = req.hasFeature
                    });
                }
                else
                {
                    existing.hasFeature = req.hasFeature;
                    unitOfWork.HotelCriteriaFeatureRepository.Update(existing);
                }

                unitOfWork.Save();
                ans.success = true;
                return Ok(ans);
            }
            catch (Exception)
            {
                return Ok(ans);
            }
        }

        [Route("api/CriteriaApi/SaveCriteria")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult SaveCriteria([FromBody]HotelCriteriaViewModel model)
        {

            // Μόνο η ενεργή (μη ολοκληρωμένη) αυτοαξιολόγηση του τρέχοντος κύκλου
            HotelCriteria hotelCriteria = unitOfWork.HotelCriteriaRepository
                .Get(x => x.hotelID == model.hotelID && x.exploitingCompanyID == model.exploitingCompanyID
                       && x.version == 1 && x.isFinished == false)
                .FirstOrDefault();

            // Δεν επιτρέπεται αποθήκευση πάνω σε οριστικά υποβεβλημένη αυτοαξιολόγηση
            if (hotelCriteria != null && hotelCriteria.status == 2)
                return Ok(new ApiAnswer() { success = false });

            if (hotelCriteria == null)
            {
                hotelCriteria = new HotelCriteria();
            }

            hotelCriteria.version = model.version;
            hotelCriteria.status = model.status;
            hotelCriteria.hotelID = model.hotelID;
            hotelCriteria.exploitingCompanyID = model.exploitingCompanyID;


            decimal totalPoints = 0;
            decimal maxPoints = 0;
            List<HotelCriteria_Criteria> savedCriteriaList = new List<HotelCriteria_Criteria>();

            // Server-side επιβολή κανόνων κεντρικών ρυθμίσεων: όσα κριτήρια απενεργοποιεί
            // η δήλωση παροχών εξαιρούνται από τη βαθμολογία, ανεξάρτητα από τον client.
            HashSet<decimal> featureDisabled = Utils.FeatureRules.GetFeatureDisabledCriteria(unitOfWork, hotelCriteria.id);

            if (model.criteria != null && model.criteria.Count > 0)
            {
                foreach (var z in model.criteria)
                {

                    HotelCriteria_Criteria criteria = unitOfWork.HotelCriteria_CriteriaRepository.Get(x => x.hotelCriteriaID == hotelCriteria.id && x.criteriaID == z.criteriaID).FirstOrDefault();

                    if (criteria == null)
                    {
                        criteria = new HotelCriteria_Criteria();
                    }

                    criteria.criteriaID = z.criteriaID;
                    criteria.hotelCriteriaID = hotelCriteria.id > 0 ? hotelCriteria.id : 0;
                    criteria.hotelCriteria = hotelCriteria;
                    criteria.isApplicable = z.isApplicable && !featureDisabled.Contains(z.criteriaID);
                    criteria.isChecked = z.isChecked;
                    criteria.isNotChecked = z.isNotChecked;
                    
                    criteria.value = z.value;

                    criteria.points = 0;
                    if (criteria.isApplicable == true)
                    {
                        Criteria crit = unitOfWork.CriteriaRepository.GetByID(z.criteriaID);
                        if (crit.criteriaType == 1 && criteria.isChecked == true)
                        {
                            criteria.points = crit.maxGrade * crit.weight ;
                            totalPoints += crit.maxGrade * crit.weight;
                            maxPoints += crit.maxGrade * crit.weight;
                        }
                        else if (crit.criteriaType == 1 )
                        {
                            criteria.points = 0;
                            maxPoints += crit.maxGrade * crit.weight;
                        }
                        else if (crit.criteriaType == 3)
                        {
                            if (criteria.isChecked == true)
                            {
                                criteria.points = crit.maxGrade * crit.weight;
                                totalPoints += crit.maxGrade * crit.weight;
                                maxPoints += crit.maxGrade * crit.weight;
                            }
                            else
                            {
                                criteria.points = 0;
                            }

                        }
                        else if (crit.criteriaType == 2)
                        {
                            decimal? value = null;
                            try
                            {
                                if (z.value != null)
                                {
                                    value = Convert.ToDecimal(z.value.Replace(".", ","));
                                    criteria.points = value * crit.weight;
                                    totalPoints += value.Value * crit.weight;
                                }
                             
                            }
                            catch(Exception e)
                            {

                            }

                            maxPoints += crit.maxGrade * crit.weight;

                        }
                        

                    }


                    if (criteria.id > 0)
                        unitOfWork.HotelCriteria_CriteriaRepository.Update(criteria);
                    else
                        unitOfWork.HotelCriteria_CriteriaRepository.Insert(criteria);

                    savedCriteriaList.Add(criteria);

                }
            }

            hotelCriteria.maxPoints = maxPoints;
            hotelCriteria.totalPoints = totalPoints;

            // Αναγμένη βαθμολογία ανά πυλώνα + απονομή (gated) μεταλλίου
            Utils.ScoringHelper.ApplyMedal(unitOfWork, hotelCriteria, savedCriteriaList);

            // Ημερομηνία πρώτης αποθήκευσης (δεν αντικαθίσταται αν υπάρχει ήδη)
            if (hotelCriteria.creationDatetime == null)
                hotelCriteria.creationDatetime = DateTime.Now;

            // Ημερομηνία οριστικής υποβολής (ενημερώνεται μόνο στην υποβολή status=2)
            if (model.status == 2)
                hotelCriteria.lastModificationDateTime = DateTime.Now;



            if (hotelCriteria.id > 0)
                unitOfWork.HotelCriteriaRepository.Update(hotelCriteria);
            else
                unitOfWork.HotelCriteriaRepository.Insert(hotelCriteria);

            try
            {

                unitOfWork.Save();


                return Ok(new ApiAnswer() { success = true });
            }
            catch(Exception e)
            {
                return Ok(new ApiAnswer() { success = false });

            }


        }



        [Route("api/CriteriaApi/GetCriteriaFIles")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetCriteriaFIles([FromBody]HotelCriteria_CriteriaFileViewModel file)
        {
            Results results = new Results();

            try
            {
                string sql = "Select * from [V_TEE_HotelCriteria_CriteriaFiles] where hotelCriteriaID = @hotelCriteriaID and criteriaFileID = @criteriaFileID";
                List<HotelCriteria_CriteriaFileViewModel> hotelCriteria_criteriaFiles = unitOfWork.context.Database
                    .SqlQuery<HotelCriteria_CriteriaFileViewModel>(sql,
                        new SqlParameter("@hotelCriteriaID", file.hotelCriteriaID),
                        new SqlParameter("@criteriaFileID", file.criteriaFileID))
                    .ToList();


                results.hotelCriteria_criteriaFiles = hotelCriteria_criteriaFiles;

                return Ok(results);
            }
            catch (Exception e)
            {

                return Ok(results);
            }


        }



        [Route("api/CriteriaApi/DeleteFile")]
        [AllowAnonymous]
        [HttpPost]
        public IHttpActionResult DeleteFile([FromBody] HotelCriteria_CriteriaFileViewModel model)
        {


            try
            {


                if (model != null && model.id > 0)
                {

                    HotelCriteria_CriteriaFile file = unitOfWork.HotelCriteria_CriteriaFileRepository.GetByID(model.id);


                    if (file == null)
                        return NotFound();



                    unitOfWork.HotelCriteria_CriteriaFileRepository.Delete(file);

                    AzureStorage.AzureStorage.RemoveFileFromFolder(file.hotelCriteriaID.ToString(), file.criteriaFileID.ToString(), file.fileName).Wait();


                    unitOfWork.Save();

                    return Ok(new ApiAnswer() { success = true });


                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception e)
            {
                return Ok(new ApiAnswer() { success = false });
            }


        }

    }
}
