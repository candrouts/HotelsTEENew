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
using System.Globalization;

namespace HotelsTEE.Controllers
{


    [Authorize]
    public class CertificateApiController : ApiController
    {

        UnitOfWork unitOfWork = new UnitOfWork();

        [Route("api/CertificateApi/GetAllCertificates")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetAllCertificates()
        {
            Results results = new Results();

            try
            {
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();

                if (user == null)
                {
                    return NotFound();
                }
                else if (user.role != 10 && user.role != 100)
                {
                    return NotFound();
                }

                // role=10: μόνο οι αναθέσεις του επιθεωρητή | role=100 (admin): όλες οι αιτήσεις
                List<CertificateViewModel> allCertificates;
                if (user.role == 100)
                {
                    sql = "Select * from V_TEE_Certificates_Inspector";
                    allCertificates = unitOfWork.context.Database
                        .SqlQuery<CertificateViewModel>(sql)
                        .ToList();
                }
                else
                {
                    sql = "Select * from V_TEE_Certificates_Inspector where UserName = @UserName";
                    allCertificates = unitOfWork.context.Database
                        .SqlQuery<CertificateViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                        .ToList();
                }

                results.user = user;

                // Μετάλλια (για φίλτρο & εμφάνιση)
                List<MedalViewModel> medals = unitOfWork.context.Database
                    .SqlQuery<MedalViewModel>("Select * from V_TEE_Medals order by min").ToList();
                results.medals = medals;

                // Γεωγραφικά (Περιφέρεια/ΠΕ) ανά κατάλυμα — μόνο για admin φίλτρα.
                // ΜΟΝΟ οι 4 απαραίτητες στήλες και ΜΟΝΟ για τα καταλύματα της λίστας
                // (αποφυγή full scan του βαρύ V_TEE_HotelDetails).
                Dictionary<string, HotelDetailsViewModel> geoByHotel = null;
                if (user.role == 100)
                {
                    const string geoCols = "Select hotelID, exploitingCompanyID, periphereiaTitle, peripheryTitle from V_TEE_HotelDetails";
                    List<string> hotelIds = allCertificates.Select(c => c.hotelID)
                        .Where(h => !string.IsNullOrEmpty(h)).Distinct().ToList();

                    List<HotelDetailsViewModel> geoList;
                    if (hotelIds.Count > 0 && hotelIds.Count <= 1800)
                    {
                        var names = new string[hotelIds.Count];
                        var pars = new SqlParameter[hotelIds.Count];
                        for (int i = 0; i < hotelIds.Count; i++)
                        {
                            names[i] = "@h" + i;
                            pars[i] = new SqlParameter("@h" + i, hotelIds[i]);
                        }
                        geoList = unitOfWork.context.Database
                            .SqlQuery<HotelDetailsViewModel>(geoCols + " where hotelID in (" + string.Join(",", names) + ")", pars)
                            .ToList();
                    }
                    else
                    {
                        geoList = unitOfWork.context.Database
                            .SqlQuery<HotelDetailsViewModel>(geoCols).ToList();
                    }

                    geoByHotel = geoList
                        .GroupBy(h => (h.hotelID ?? "") + "|" + (h.exploitingCompanyID ?? ""))
                        .ToDictionary(g => g.Key, g => g.First());
                }

                // ── Εμπλουτισμός με workflow state ανά αίτηση ─────────────────
                List<decimal> certIds = allCertificates.Select(c => c.certificateID).ToList();

                List<HotelCriteria> allHotelCriteria = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID.HasValue && certIds.Contains(x.certificateID.Value))
                    .ToList();

                List<HotelierCertificate> certEntities = unitOfWork.HotelierCertificateRepository
                    .Get(x => certIds.Contains(x.certificateID))
                    .ToList();

                foreach (var c in allCertificates)
                {
                    HotelCriteria v2 = allHotelCriteria.FirstOrDefault(x => x.certificateID == c.certificateID && x.version == 2);
                    HotelCriteria v3 = allHotelCriteria.FirstOrDefault(x => x.certificateID == c.certificateID && x.version == 3);
                    HotelierCertificate ent = certEntities.FirstOrDefault(x => x.certificateID == c.certificateID);

                    c.v2Status = v2 != null ? (int?)v2.status : null;
                    c.v3Status = v3 != null ? (int?)v3.status : null;
                    c.autopsyDateStatus = ent != null ? ent.autopsyDateStatus : null;

                    bool dateArrived = ent != null
                                       && ent.autopsyDateTime.HasValue
                                       && ent.autopsyDateTime.Value.Date <= DateTime.Today;

                    // Αυτοψία: ενεργή όταν έχει έρθει η ημ/νία (ή έχει ήδη ξεκινήσει v2)
                    c.canDoAutopsy = v2 != null || dateArrived;

                    // Τελική κατάταξη: ενεργή όταν η αυτοψία υποβλήθηκε οριστικά
                    c.canDoFinal = v2 != null && v2.status == 2;

                    // Νέα ανάθεση: δεν έχει ξεκινήσει αυτοψία και εκκρεμεί απάντηση στην ημ/νία
                    c.isNew = v2 == null && (ent == null || ent.autopsyDateStatus == null || ent.autopsyDateStatus == 1);

                    // Έφτασε/πέρασε η ημ/νία αυτοψίας και δεν έχει υποβληθεί η αυτοψία
                    c.isAutopsyDue = dateArrived && (v2 == null || v2.status != 2);

                    // Έκδοση βεβαίωσης + σχόλιο απόρριψης ΤΕΛΙΚΗΣ ΚΑΤΑΤΑΞΗΣ
                    // (όχι το ιστορικό απόρριψης ανάθεσης που μένει στο ίδιο πεδίο notes)
                    c.isIssued = ent != null && ent.certificateStatusID == 2;
                    c.rejectionNote = Utils.CertificateNotes.ExtractFinalRejection(ent != null ? ent.notes : null);

                    // Μετάλλιο από την τελική κατάταξη (v3)
                    if (v3 != null && v3.medalID.HasValue)
                    {
                        c.medalID = v3.medalID;
                        MedalViewModel m = medals.FirstOrDefault(x => x.id == v3.medalID.Value);
                        c.medalTitle = m != null ? m.title : null;
                    }

                    // Περιφέρεια/ΠΕ (admin)
                    if (geoByHotel != null)
                    {
                        HotelDetailsViewModel hd;
                        if (geoByHotel.TryGetValue((c.hotelID ?? "") + "|" + (c.exploitingCompanyID ?? ""), out hd))
                        {
                            c.periphereiaTitle = hd.periphereiaTitle;
                            c.peripheryTitle = hd.peripheryTitle;
                        }
                    }
                }

                results.certificates = allCertificates;


                return Ok(results);
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");

                return Ok(results);
            }


        }


        // mode: 1 = προβολή αυτοαξιολόγησης (v1, read-only, χωρίς δημιουργία)
        //       2 = αυτοψία (v2 — δημιουργείται από v1 αν δεν υπάρχει)
        //       3 = τελική κατάταξη (v3 — δημιουργείται από v2 αν δεν υπάρχει)
        [Route("api/CertificateApi/GetCertificate/{id:decimal}/{mode:int?}")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetCertificate(decimal id, int mode = 2)
        {
            Results results = new Results();

            if (mode < 1 || mode > 3) mode = 2;

            try
            {
                string sql = "Select * from V_TEE_Certificates_Inspector where certificateID = @certificateID";
                CertificateViewModel certificate = unitOfWork.context.Database
                    .SqlQuery<CertificateViewModel>(sql, new SqlParameter("@certificateID", id))
                    .FirstOrDefault();

                // Fallback: το view μπορεί να μην περιλαμβάνει ολοκληρωμένες/εκδομένες αιτήσεις —
                // φορτώνουμε τα βασικά στοιχεία από τον πίνακα HotelierCertificates
                if (certificate == null)
                {
                    HotelierCertificate certEntity = unitOfWork.HotelierCertificateRepository.GetByID(id);
                    if (certEntity == null)
                        return NotFound();

                    certificate = new CertificateViewModel
                    {
                        certificateID = certEntity.certificateID,
                        hotelID = certEntity.hotelID,
                        exploitingCompanyID = certEntity.exploitingCompanyID,
                        autopsyDateTime = certEntity.autopsyDateTime.HasValue
                            ? certEntity.autopsyDateTime.Value.ToString("dd/MM/yyyy") : null
                    };
                }

                sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
          
                sql = "Select * from V_TEE_Categories where isActive=1 order by [order] ";
                List<CategoryViewModel> allCategories = unitOfWork.context.Database.SqlQuery<CategoryViewModel>(sql).ToList();

                sql = "Select * from V_TEE_Criteria where dateFrom <= getdate() AND dateTo >= getDate() order by [order] ";
                List<CriteriaViewModel> allCriteria = unitOfWork.context.Database.SqlQuery<CriteriaViewModel>(sql).ToList();

                sql = "Select * from V_TEE_HotelDetails where hotelID = @hotelID and exploitingCompanyID = @companyID";
                HotelDetailsViewModel hotelDetails = unitOfWork.context.Database
                    .SqlQuery<HotelDetailsViewModel>(sql,
                        new SqlParameter("@hotelID", certificate.hotelID),
                        new SqlParameter("@companyID", certificate.exploitingCompanyID))
                    .FirstOrDefault();

                List<CategoryViewModel> categories = allCategories.Where(x => !x.parentID.HasValue).OrderBy(x => x.order).ToList();

                sql = "Select * from V_TEE_Criteria_Files where isActive=1 ";
                List<CriteriaFileViewModel> allCriteriaFiles = unitOfWork.context.Database.SqlQuery<CriteriaFileViewModel>(sql).ToList();

                // Ξενοδόχος (role=1): πρόσβαση ΜΟΝΟ στο δικό του certificate, πάντα read-only (χωρίς clone)
                bool isHotelierViewer = user != null && user.role == 1;
                if (isHotelierViewer)
                {
                    string sqlOwn = "Select * from V_TEE_HotelDetails where UserName = @UserName";
                    HotelDetailsViewModel ownHotel = unitOfWork.context.Database
                        .SqlQuery<HotelDetailsViewModel>(sqlOwn, new SqlParameter("@UserName", User.Identity.Name))
                        .FirstOrDefault();

                    if (ownHotel == null
                        || ownHotel.hotelID != certificate.hotelID
                        || ownHotel.exploitingCompanyID != certificate.exploitingCompanyID)
                        return NotFound();
                }

                sql = "Select * from V_TEE_HotelCriteria where certificateID = @certificateID and version = @version";
                HotelCriteriaViewModel hotelCriteria = unitOfWork.context.Database
                    .SqlQuery<HotelCriteriaViewModel>(sql,
                        new SqlParameter("@certificateID", certificate.certificateID),
                        new SqlParameter("@version", mode))
                    .FirstOrDefault();

                // mode 1 = προβολή: δεν δημιουργούμε ποτέ εγγραφή.
                // Ξενοδόχος: ποτέ clone — βλέπει μόνο ό,τι υπάρχει.
                // mode 2/3 (επιθεωρητής/admin): αν δεν υπάρχει η έκδοση, κλωνοποιούμε από την προηγούμενη,
                //           με προϋπόθεση η προηγούμενη να είναι οριστικά υποβεβλημένη (status=2).
                if (hotelCriteria == null && mode > 1 && !isHotelierViewer)
                {
                    try
                    {
                        int sourceVersion = mode - 1;

                        HotelCriteria oldHotelCriteria = unitOfWork.HotelCriteriaRepository
                            .Get(x => x.certificateID == certificate.certificateID && x.version == sourceVersion && x.status == 2)
                            .First();
                        List<HotelCriteria_Criteria> oldHotelCriteria_Criteria = unitOfWork.HotelCriteria_CriteriaRepository.Get(x => x.hotelCriteriaID == oldHotelCriteria.id).ToList();

                        HotelCriteria hotCrit = new HotelCriteria();

                        hotCrit.status = 1;
                        hotCrit.version = mode;
                        hotCrit.certificateID = id;
                        hotCrit.exploitingCompanyID = hotelDetails.exploitingCompanyID;
                        hotCrit.hotelID = hotelDetails.hotelID;
                        hotCrit.maxPoints = oldHotelCriteria.maxPoints;
                        hotCrit.medalID = oldHotelCriteria.medalID;
                        hotCrit.totalPoints = oldHotelCriteria.totalPoints;
                        hotCrit.creationDatetime = DateTime.Now;

                        foreach (var z in oldHotelCriteria_Criteria)
                        {
                            HotelCriteria_Criteria newCrit = new HotelCriteria_Criteria();
                            newCrit.criteriaID = z.criteriaID;
                            newCrit.hotelCriteria = hotCrit;
                            newCrit.isApplicable = z.isApplicable;
                            newCrit.isChecked = z.isChecked;
                            newCrit.isNotChecked = z.isNotChecked;
                            newCrit.points = z.points;
                            newCrit.value = z.value;

                            unitOfWork.HotelCriteria_CriteriaRepository.Insert(newCrit);
                        }

                        unitOfWork.HotelCriteriaRepository.Insert(hotCrit);

                        unitOfWork.Save();

                        // ── Αντιγραφή απαντήσεων κεντρικών ρυθμίσεων (v1→v2, v2→v3) ──
                        // Ο επιθεωρητής ξεκινά από τη δήλωση του ξενοδόχου και μπορεί να τη διορθώσει.
                        try
                        {
                            List<HotelCriteriaFeature> oldFeats = unitOfWork.HotelCriteriaFeatureRepository
                                .Get(x => x.hotelCriteriaID == oldHotelCriteria.id).ToList();
                            foreach (var fa in oldFeats)
                            {
                                unitOfWork.HotelCriteriaFeatureRepository.Insert(new HotelCriteriaFeature
                                {
                                    hotelCriteriaID = hotCrit.id,
                                    featureID = fa.featureID,
                                    hasFeature = fa.hasFeature
                                });
                            }
                            if (oldFeats.Count > 0)
                                unitOfWork.Save();
                        }
                        catch (Exception exLog)
                        { HotelsTEE.Utils.ErrorLogger.Log(exLog, "CertificateApiController.cs");
                            // Δεν μπλοκάρει τη δημιουργία της νέας έκδοσης
                        }

                        // ── Αντιγραφή τεκμηρίων από την προηγούμενη έκδοση (π.χ. v2 → v3) ──
                        // Metadata στον πίνακα + φυσικά αρχεία στο Azure File Share.
                        try
                        {
                            List<HotelCriteria_CriteriaFile> oldFiles = unitOfWork.HotelCriteria_CriteriaFileRepository
                                .Get(x => x.hotelCriteriaID == oldHotelCriteria.id).ToList();

                            foreach (var f in oldFiles)
                            {
                                HotelCriteria_CriteriaFile newFile = new HotelCriteria_CriteriaFile();
                                newFile.hotelCriteriaID = hotCrit.id;
                                newFile.criteriaFileID = f.criteriaFileID;
                                newFile.fileName = f.fileName;
                                newFile.fileType = f.fileType;
                                newFile.creationDateTime = DateTime.Now;

                                unitOfWork.HotelCriteria_CriteriaFileRepository.Insert(newFile);

                                AzureStorage.AzureStorage.CopyFileBetweenCriteria(
                                    oldHotelCriteria.id.ToString(),
                                    hotCrit.id.ToString(),
                                    f.criteriaFileID.ToString(),
                                    f.fileName);
                            }

                            if (oldFiles.Count > 0)
                                unitOfWork.Save();
                        }
                        catch (Exception exLog)
                        { HotelsTEE.Utils.ErrorLogger.Log(exLog, "CertificateApiController.cs");
                            // Η αντιγραφή τεκμηρίων δεν πρέπει να μπλοκάρει τη δημιουργία της νέας έκδοσης
                        }


                        sql = "Select * from V_TEE_HotelCriteria where certificateID = @certificateID and version = @version";
                        hotelCriteria = unitOfWork.context.Database
                            .SqlQuery<HotelCriteriaViewModel>(sql,
                                new SqlParameter("@certificateID", certificate.certificateID),
                                new SqlParameter("@version", mode))
                            .FirstOrDefault();

                    }
                    catch (Exception e)
                    { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");

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
                            else
                            {
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

                // Κεντρικές ρυθμίσεις/παροχές (ενεργές) + αντιστοιχίσεις + απαντήσεις της έκδοσης
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


                if (user == null)
                {
                    return NotFound();
                }
                else if (user.role != 10 && user.role != 100 && user.role != 1)
                {
                    return NotFound();
                }

                results.user = user;

                results.certificate = certificate;


                return Ok(results);
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");

                return Ok(results);
            }


        }


        // Αποθήκευση/διόρθωση απάντησης κεντρικής ρύθμισης από τον επιθεωρητή (ή admin),
        // πάνω στην έκδοση αυτοψίας (v2). Έλεγχος ότι ο χρήστης είναι ο ανατεθειμένος επιθεωρητής.
        [Route("api/CertificateApi/SaveFeatureAnswer")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult SaveFeatureAnswer([FromBody] FeatureAnswerSaveRequest req)
        {
            var ans = new ApiAnswer { success = false };
            try
            {
                if (req == null || req.hotelCriteriaID <= 0 || req.featureID <= 0)
                    return Ok(ans);

                HotelCriteria hc = unitOfWork.HotelCriteriaRepository.GetByID(req.hotelCriteriaID);
                if (hc == null || !hc.certificateID.HasValue) return Ok(ans);

                string sql = "Select * from V_TEE_Users where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (user == null) return Ok(ans);

                HotelierCertificate cert = unitOfWork.HotelierCertificateRepository.GetByID(hc.certificateID.Value);
                if (cert == null) return Ok(ans);

                bool allowed = user.role == 100
                    || (user.role == 10 && user.tee_inspectorID.HasValue && cert.tee_inspectorID == user.tee_inspectorID.Value);
                if (!allowed) return Ok(ans);

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
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "CertificateApiController.cs");
                return Ok(ans);
            }
        }

        [Route("api/CertificateApi/SaveCriteria")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult SaveCriteria([FromBody] HotelCriteriaViewModel model)
        {

            // Δεν επιτρέπεται αποθήκευση πάνω στην αυτοαξιολόγηση του ξενοδόχου (v1)
            if (model.version < 2 || model.version > 3)
                return Ok(new ApiAnswer() { success = false });

            HotelCriteria hotelCriteria = unitOfWork.HotelCriteriaRepository.Get(x => x.hotelID == model.hotelID && x.exploitingCompanyID == model.exploitingCompanyID && x.certificateID == model.certificateID && x.version == model.version).FirstOrDefault();

            // Δεν επιτρέπεται αποθήκευση πάνω σε οριστικά υποβεβλημένη (status=2)
            // ή ολοκληρωμένη (isFinished) έκδοση. (Μετά από απόρριψη ξενοδόχου η v3 γυρνά σε status=1.)
            if (hotelCriteria != null && (hotelCriteria.status == 2 || hotelCriteria.isFinished))
                return Ok(new ApiAnswer() { success = false });

            if (hotelCriteria == null)
            {
                hotelCriteria = new HotelCriteria();
            }

            hotelCriteria.version = model.version;
            hotelCriteria.status = model.status;
            hotelCriteria.hotelID = model.hotelID;
            hotelCriteria.exploitingCompanyID = model.exploitingCompanyID;
            hotelCriteria.certificateID = model.certificateID;

            decimal totalPoints = 0;
            decimal maxPoints = 0;
            List<HotelCriteria_Criteria> savedCriteriaList = new List<HotelCriteria_Criteria>();

            // Server-side επιβολή κανόνων κεντρικών ρυθμίσεων (βάσει της δήλωσης παροχών
            // αυτής της έκδοσης — π.χ. όπως τη διόρθωσε ο επιθεωρητής στην αυτοψία).
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
                            criteria.points = crit.maxGrade * crit.weight;
                            totalPoints += crit.maxGrade * crit.weight;
                            maxPoints += crit.maxGrade * crit.weight;
                        }
                        else if (crit.criteriaType == 1)
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
                            catch (Exception e)
                            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");

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

            // Ημερομηνίες (όπως και στην αυτοαξιολόγηση)
            if (hotelCriteria.creationDatetime == null)
                hotelCriteria.creationDatetime = DateTime.Now;
            if (model.status == 2)
                hotelCriteria.lastModificationDateTime = DateTime.Now;

            //if (hotelCriteria.status == 2)
            //{
            //    HotelierCertificate certificate = new HotelierCertificate();

            //    certificate.autopsyDateTime = DateTime.Now.AddDays(4);
            //    certificate.certificateStatusID = 23;
            //    certificate.certificateTypeID = 84;
            //    certificate.hotelID = hotelCriteria.hotelID;
            //    certificate.exploitingCompanyID = hotelCriteria.exploitingCompanyID;
            //    certificate.inspector = unitOfWork.InspectorRepository.Get().LastOrDefault();
            //    certificate.tee_inspectorID = certificate.inspector.id;
            //    certificate.creationDateTime = DateTime.Now;
            //    certificate.responsibleUserID = 3;

            //    unitOfWork.HotelierCertificateRepository.Insert(certificate);

            //    hotelCriteria.certificate = certificate;
            //}




            if (hotelCriteria.id > 0)
                unitOfWork.HotelCriteriaRepository.Update(hotelCriteria);
            else
                unitOfWork.HotelCriteriaRepository.Insert(hotelCriteria);

            try
            {

                unitOfWork.Save();

                // Ειδοποίηση: οριστική υποβολή τελικής κατάταξης (v3) → ξενοδόχος για αποδοχή
                if (model.version == 3 && model.status == 2 && hotelCriteria.certificateID.HasValue)
                {
                    string medalTitle = "-";
                    if (hotelCriteria.medalID.HasValue)
                    {
                        Medal medal = unitOfWork.MedalRepository.GetByID(hotelCriteria.medalID.Value);
                        if (medal != null) medalTitle = medal.title;
                    }

                    Utils.NotificationService.Fire("FINAL_SUBMITTED", hotelCriteria.certificateID.Value,
                        new Dictionary<string, string> {
                            { "hotelName", Utils.NotificationService.HotelName(hotelCriteria.certificateID.Value) },
                            { "inspectorName", User.Identity.Name },
                            { "points", (hotelCriteria.totalScore ?? hotelCriteria.totalPoints).ToString("0.##") },
                            { "medal", medalTitle },
                            { "link", Utils.NotificationService.Link("/InspectorSelection") }
                        });
                }

                return Ok(new ApiAnswer() { success = true });
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");
                return Ok(new ApiAnswer() { success = false });

            }


        }

        // Βοηθητικό: όνομα του ανατεθειμένου επιθεωρητή μιας αίτησης
        private string InspectorName(HotelierCertificate certificate)
        {
            if (certificate == null || !certificate.tee_inspectorID.HasValue) return "";
            Inspector insp = unitOfWork.InspectorRepository.GetByID(certificate.tee_inspectorID.Value);
            return insp != null ? insp.firstName + " " + insp.lastName : "";
        }

        // ---------------------------------------------------------------
        // NEW ENDPOINT: Inline update of autopsyDateTime
        // ---------------------------------------------------------------
        [Route("api/CertificateApi/UpdateAutopsyDate")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult UpdateAutopsyDate([FromBody] UpdateAutopsyDateViewModel model)
        {
            try
            {
                if (model == null || model.certificateID <= 0)
                    return BadRequest("Invalid data.");

                // Parse the incoming date string (expected format: dd/MM/yyyy)
                DateTime parsedDate;
                if (!DateTime.TryParseExact(
                        model.autopsyDateTime,
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out parsedDate))
                {
                    return Ok(new { success = false, message = "Μη έγκυρη μορφή ημερομηνίας. Χρησιμοποιήστε dd/MM/yyyy." });
                }

                HotelierCertificate certificate = unitOfWork.HotelierCertificateRepository
                    .GetByID(model.certificateID);

                if (certificate == null)
                    return NotFound();

                certificate.autopsyDateTime = parsedDate;

                // Αλλαγή ημ/νίας από επιθεωρητή = οριστικοποίηση με νέα ημ/νία (ένας γύρος)
                certificate.autopsyDateStatus = 3;
                certificate.autopsyDateConfirmationDateTime = DateTime.Now;

                unitOfWork.HotelierCertificateRepository.Update(certificate);
                unitOfWork.Save();

                // Ειδοποίηση: νέα ημ/νία αυτοψίας → ξενοδόχος
                Utils.NotificationService.Fire("AUTOPSY_DATE_CHANGED", certificate.certificateID,
                    new Dictionary<string, string> {
                        { "hotelName", Utils.NotificationService.HotelName(certificate.certificateID) },
                        { "inspectorName", InspectorName(certificate) },
                        { "date", parsedDate.ToString("dd/MM/yyyy") },
                        { "link", Utils.NotificationService.Link("/InspectorSelection") }
                    });

                return Ok(new  { success = true, message = "Η ημερομηνία αυτοψίας ενημερώθηκε και οριστικοποιήθηκε." });
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");
                return Ok(new { success = false, message = e.Message });
            }
        }

        // ---------------------------------------------------------------
        // Αποδοχή της προτεινόμενης ημ/νίας αυτοψίας από τον επιθεωρητή
        // ---------------------------------------------------------------
        [Route("api/CertificateApi/AcceptAutopsyDate")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult AcceptAutopsyDate([FromBody] UpdateAutopsyDateViewModel model)
        {
            try
            {
                if (model == null || model.certificateID <= 0)
                    return BadRequest("Invalid data.");

                HotelierCertificate certificate = unitOfWork.HotelierCertificateRepository
                    .GetByID(model.certificateID);

                if (certificate == null)
                    return NotFound();

                if (!certificate.autopsyDateTime.HasValue)
                    return Ok(new { success = false, message = "Δεν υπάρχει προτεινόμενη ημερομηνία προς αποδοχή." });

                certificate.autopsyDateStatus = 2;  // 2 = αποδοχή από επιθεωρητή
                certificate.autopsyDateConfirmationDateTime = DateTime.Now;

                unitOfWork.HotelierCertificateRepository.Update(certificate);
                unitOfWork.Save();

                // Ειδοποίηση: αποδοχή ημ/νίας → ξενοδόχος
                Utils.NotificationService.Fire("AUTOPSY_DATE_ACCEPTED", certificate.certificateID,
                    new Dictionary<string, string> {
                        { "hotelName", Utils.NotificationService.HotelName(certificate.certificateID) },
                        { "inspectorName", InspectorName(certificate) },
                        { "date", certificate.autopsyDateTime.HasValue ? certificate.autopsyDateTime.Value.ToString("dd/MM/yyyy") : "-" },
                        { "link", Utils.NotificationService.Link("/InspectorSelection") }
                    });

                return Ok(new { success = true, message = "Η ημερομηνία αυτοψίας έγινε αποδεκτή." });
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");
                return Ok(new { success = false, message = e.Message });
            }
        }

        // ── Απόρριψη ανάθεσης από επιθεωρητή ────────────────────────────
        // Επιτρέπεται ΜΟΝΟ πριν ξεκινήσει η αυτοψία (πριν υπάρξει v2).
        // Το certificate γίνεται status=24 (απορρίφθηκε) → φεύγει από τη λίστα
        // του επιθεωρητή και ο ξενοδόχος επιλέγει νέο επιθεωρητή (reuse).
        [Route("api/CertificateApi/RejectAssignment")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult RejectAssignment([FromBody] RejectAssignmentViewModel model)
        {
            try
            {
                if (model == null || model.certificateID <= 0)
                    return Ok(new { success = false, message = "Μη έγκυρα δεδομένα." });

                if (string.IsNullOrWhiteSpace(model.comment))
                    return Ok(new { success = false, message = "Το σχόλιο απόρριψης είναι υποχρεωτικό." });

                // Έλεγχος: ο logged-in είναι ο ανατεθειμένος επιθεωρητής
                string sql = "Select * from [V_TEE_Users] where UserName = @UserName";
                UserViewModel user = unitOfWork.context.Database
                    .SqlQuery<UserViewModel>(sql, new SqlParameter("@UserName", User.Identity.Name))
                    .FirstOrDefault();
                if (user == null || user.role != 10 || !user.tee_inspectorID.HasValue)
                    return Ok(new { success = false, message = "Δεν έχετε δικαίωμα." });

                HotelierCertificate certificate = unitOfWork.HotelierCertificateRepository
                    .GetByID(model.certificateID);
                if (certificate == null)
                    return NotFound();

                if (certificate.tee_inspectorID != user.tee_inspectorID.Value)
                    return Ok(new { success = false, message = "Η ανάθεση δεν σας αφορά." });

                // Δεν επιτρέπεται αν έχει ξεκινήσει η αυτοψία (υπάρχει v2)
                decimal certID = certificate.certificateID;
                bool autopsyStarted = unitOfWork.HotelCriteriaRepository
                    .Get(x => x.certificateID == certID && x.version == 2)
                    .Any();
                if (autopsyStarted)
                    return Ok(new { success = false, message = "Δεν επιτρέπεται απόρριψη — η αυτοψία έχει ήδη ξεκινήσει." });

                if (certificate.certificateStatusID != 23)
                    return Ok(new { success = false, message = "Η ανάθεση δεν είναι σε κατάσταση που επιτρέπει απόρριψη." });

                // Ιστορικό απόρριψης στο notes (με επιθεωρητή + timestamp)
                Inspector insp = unitOfWork.InspectorRepository.GetByID(user.tee_inspectorID.Value);
                string inspName = insp != null ? insp.firstName + " " + insp.lastName : "Επιθεωρητής";
                string entry = "[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "] Απόρριψη ανάθεσης από " +
                               inspName + ": " + model.comment.Trim();
                certificate.notes = string.IsNullOrEmpty(certificate.notes)
                    ? entry
                    : certificate.notes + Environment.NewLine + entry;

                certificate.certificateStatusID = 24;  // Απορρίφθηκε από επιθεωρητή

                unitOfWork.HotelierCertificateRepository.Update(certificate);
                unitOfWork.Save();

                // Ειδοποίηση: απόρριψη ανάθεσης → ξενοδόχος
                Utils.NotificationService.Fire("ASSIGNMENT_REJECTED", certificate.certificateID,
                    new Dictionary<string, string> {
                        { "hotelName", Utils.NotificationService.HotelName(certificate.certificateID) },
                        { "inspectorName", inspName },
                        { "comment", model.comment.Trim() },
                        { "link", Utils.NotificationService.Link("/InspectorSelection") }
                    });

                return Ok(new { success = true, message = "Η ανάθεση απορρίφθηκε." });
            }
            catch (Exception e)
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");
                return Ok(new { success = false, message = e.Message });
            }
        }

        [Route("api/CertificateApi/GetCriteriaFIles")]
        [Authorize]
        [HttpPost]
        public IHttpActionResult GetCriteriaFIles([FromBody] HotelCriteria_CriteriaFileViewModel file)
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
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");

                return Ok(results);
            }


        }



        [Route("api/CertificateApi/DeleteFile")]
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



                    // Καθαρισμός και των αποτελεσμάτων AI ελέγχου του τεκμηρίου
                    var aiChecks = unitOfWork.AiDocumentCheckRepository
                        .Get(x => x.hotelCriteriaFileID == model.id).ToList();
                    foreach (var c in aiChecks)
                        unitOfWork.AiDocumentCheckRepository.Delete(c);

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
            { HotelsTEE.Utils.ErrorLogger.Log(e, "CertificateApiController.cs");
                return Ok(new ApiAnswer() { success = false });
            }


        }




    }
}
