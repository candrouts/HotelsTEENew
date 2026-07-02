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
    // Διαχείριση Πυλώνων / Υποπυλώνων (TEE_Categories) & Κριτηρίων (TEE_Criteria) — admin.
    [Authorize]
    public class AdminCriteriaApiController : ApiController
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

        private static string D(DateTime d) { return d.ToString("yyyy-MM-dd"); }

        [Route("api/AdminCriteriaApi/GetTree")]
        [HttpPost]
        public IHttpActionResult GetTree()
        {
            var result = new AdminCriteriaTreeVM { success = false, pillars = new List<AdminPillarVM>() };
            try
            {
                if (!IsAdmin()) return Ok(result);

                List<CriteriaCategory> cats = unitOfWork.CriteriaCategoryRepository.Get().ToList();
                List<Criteria> crits = unitOfWork.CriteriaRepository.Get().ToList();

                // Ποια κριτήρια χρησιμοποιούνται σε αξιολογήσεις (για ασφαλή διαγραφή)
                var usedIds = new HashSet<decimal>(unitOfWork.context.Database
                    .SqlQuery<decimal>("SELECT DISTINCT criteriaID FROM TEE_HotelCriteria_Criteria"));

                // Πλήθος τεκμηρίων ανά κριτήριο
                Dictionary<decimal, int> filesByCrit = unitOfWork.Criteria_FileRepository.Get()
                    .GroupBy(f => f.criteriaID)
                    .ToDictionary(g => g.Key, g => g.Count());

                DateTime today = DateTime.Today;

                Func<Criteria, AdminCriterionVM> mapCrit = c => new AdminCriterionVM
                {
                    id = c.id,
                    categoryID = c.categoryID,
                    code = c.code,
                    title = c.title,
                    description = c.description,
                    order = c.order,
                    weight = c.weight,
                    maxGrade = c.maxGrade,
                    criteriaType = c.criteriaType,
                    gradesList = c.gradesList,
                    gradesOptions = c.gradesOptions,
                    selectList = c.selectList,
                    notes1 = c.notes1,
                    notes2 = c.notes2,
                    needsFiles = c.needsFiles ?? false,
                    notApplicable = c.notApplicable ?? false,
                    isRequired = c.isRequired ?? false,
                    dateFrom = D(c.dateFrom),
                    dateTo = D(c.dateTo),
                    isActiveNow = c.dateFrom.Date <= today && c.dateTo.Date >= today,
                    inUse = usedIds.Contains(c.id),
                    filesCount = filesByCrit.ContainsKey(c.id) ? filesByCrit[c.id] : 0
                };

                var pillars = cats.Where(x => !x.parentID.HasValue)
                    .OrderBy(x => x.order).ThenBy(x => x.id).ToList();

                foreach (var p in pillars)
                {
                    var subs = cats.Where(x => x.parentID == p.id)
                        .OrderBy(x => x.order).ThenBy(x => x.id).ToList();

                    var pillarVM = new AdminPillarVM
                    {
                        id = p.id, title = p.title, description = p.description, examples = p.examples,
                        order = p.order, totalUnits = p.totalUnits, maxGrade = p.maxGrade, isActive = p.isActive,
                        canDelete = subs.Count == 0,
                        subPillars = new List<AdminSubPillarVM>()
                    };

                    foreach (var s in subs)
                    {
                        var subCrits = crits.Where(c => c.categoryID == s.id)
                            .OrderBy(c => c.order).ThenBy(c => c.id).Select(mapCrit).ToList();

                        pillarVM.subPillars.Add(new AdminSubPillarVM
                        {
                            id = s.id, parentID = s.parentID, title = s.title, description = s.description,
                            examples = s.examples, order = s.order, totalUnits = s.totalUnits,
                            maxGrade = s.maxGrade, isActive = s.isActive,
                            canDelete = subCrits.Count == 0,
                            criteria = subCrits
                        });
                    }

                    result.pillars.Add(pillarVM);
                }

                result.success = true;
                return Ok(result);
            }
            catch (Exception exLog)
            { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs");
                return Ok(result);
            }
        }

        // ── Κατηγορία (Πυλώνας/Υποπυλώνας) ──────────────────────────────
        [Route("api/AdminCriteriaApi/SaveCategory")]
        [HttpPost]
        public IHttpActionResult SaveCategory([FromBody] CategorySaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin()) { res.message = "Δεν επιτρέπεται."; return Ok(res); }
                if (req == null || string.IsNullOrWhiteSpace(req.title)) { res.message = "Συμπληρώστε τίτλο."; return Ok(res); }

                CriteriaCategory c;
                if (req.id.HasValue && req.id.Value > 0)
                {
                    c = unitOfWork.CriteriaCategoryRepository.GetByID(req.id.Value);
                    if (c == null) { res.message = "Δεν βρέθηκε."; return Ok(res); }
                }
                else
                {
                    c = new CriteriaCategory();
                    unitOfWork.CriteriaCategoryRepository.Insert(c);
                }

                c.title = req.title.Trim();
                c.description = req.description;
                c.examples = req.examples;
                c.order = req.order;
                c.totalUnits = req.totalUnits;
                c.maxGrade = req.maxGrade;
                c.isActive = req.isActive;
                c.parentID = req.parentID;   // null = πυλώνας

                unitOfWork.Save();
                res.success = true; res.id = c.id;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); res.message = "Σφάλμα αποθήκευσης."; return Ok(res); }
        }

        [Route("api/AdminCriteriaApi/ToggleCategory")]
        [HttpPost]
        public IHttpActionResult ToggleCategory([FromBody] CategorySaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin() || req == null || !req.id.HasValue) return Ok(res);
                CriteriaCategory c = unitOfWork.CriteriaCategoryRepository.GetByID(req.id.Value);
                if (c == null) return Ok(res);
                c.isActive = req.isActive;
                unitOfWork.CriteriaCategoryRepository.Update(c);
                unitOfWork.Save();
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); return Ok(res); }
        }

        [Route("api/AdminCriteriaApi/DeleteCategory")]
        [HttpPost]
        public IHttpActionResult DeleteCategory([FromBody] CategorySaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin() || req == null || !req.id.HasValue) return Ok(res);
                decimal id = req.id.Value;

                bool hasChildren = unitOfWork.CriteriaCategoryRepository.Get(x => x.parentID == id).Any();
                bool hasCriteria = unitOfWork.CriteriaRepository.Get(x => x.categoryID == id).Any();
                if (hasChildren || hasCriteria)
                {
                    res.message = "Δεν διαγράφεται: περιέχει υποπυλώνες ή κριτήρια. Χρησιμοποιήστε «Απενεργοποίηση».";
                    return Ok(res);
                }

                CriteriaCategory c = unitOfWork.CriteriaCategoryRepository.GetByID(id);
                if (c != null) { unitOfWork.CriteriaCategoryRepository.Delete(c); unitOfWork.Save(); }
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); res.message = "Σφάλμα διαγραφής."; return Ok(res); }
        }

        // ── Κριτήριο ────────────────────────────────────────────────────
        [Route("api/AdminCriteriaApi/SaveCriterion")]
        [HttpPost]
        public IHttpActionResult SaveCriterion([FromBody] CriterionSaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin()) { res.message = "Δεν επιτρέπεται."; return Ok(res); }
                if (req == null || string.IsNullOrWhiteSpace(req.title) || string.IsNullOrWhiteSpace(req.code))
                { res.message = "Συμπληρώστε κωδικό και τίτλο."; return Ok(res); }
                if (req.categoryID <= 0) { res.message = "Λείπει ο υποπυλώνας."; return Ok(res); }

                DateTime dFrom, dTo;
                if (!DateTime.TryParse(req.dateFrom, out dFrom)) dFrom = DateTime.Today;
                if (!DateTime.TryParse(req.dateTo, out dTo)) dTo = new DateTime(2099, 12, 31);

                Criteria c;
                if (req.id.HasValue && req.id.Value > 0)
                {
                    c = unitOfWork.CriteriaRepository.GetByID(req.id.Value);
                    if (c == null) { res.message = "Δεν βρέθηκε."; return Ok(res); }
                }
                else
                {
                    c = new Criteria();
                    unitOfWork.CriteriaRepository.Insert(c);
                }

                c.categoryID = req.categoryID;
                c.code = req.code.Trim();
                c.title = req.title.Trim();
                c.description = req.description;
                c.order = req.order;
                c.weight = req.weight;
                c.maxGrade = req.maxGrade ?? 0;
                c.criteriaType = req.criteriaType;
                c.gradesList = req.gradesList;
                c.gradesOptions = req.gradesOptions;
                c.selectList = req.selectList;
                c.notes1 = req.notes1;
                c.notes2 = req.notes2;
                c.needsFiles = req.needsFiles;
                c.notApplicable = req.notApplicable;
                c.isRequired = req.isRequired;
                c.dateFrom = dFrom;
                c.dateTo = dTo;

                unitOfWork.Save();
                res.success = true; res.id = c.id;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); res.message = "Σφάλμα αποθήκευσης."; return Ok(res); }
        }

        // Ενεργοποίηση/απενεργοποίηση κριτηρίου μέσω παραθύρου ισχύος (dateTo)
        [Route("api/AdminCriteriaApi/SetCriterionActive")]
        [HttpPost]
        public IHttpActionResult SetCriterionActive([FromBody] CriterionSaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin() || req == null || !req.id.HasValue) return Ok(res);
                Criteria c = unitOfWork.CriteriaRepository.GetByID(req.id.Value);
                if (c == null) return Ok(res);

                if (req.active)
                {
                    if (c.dateFrom.Date > DateTime.Today) c.dateFrom = DateTime.Today;
                    c.dateTo = new DateTime(2099, 12, 31);
                }
                else
                {
                    c.dateTo = DateTime.Today.AddDays(-1);
                }
                unitOfWork.CriteriaRepository.Update(c);
                unitOfWork.Save();
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); return Ok(res); }
        }

        [Route("api/AdminCriteriaApi/DeleteCriterion")]
        [HttpPost]
        public IHttpActionResult DeleteCriterion([FromBody] CriterionSaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin() || req == null || !req.id.HasValue) return Ok(res);
                decimal id = req.id.Value;

                bool inUse = unitOfWork.context.Database
                    .SqlQuery<int>("SELECT COUNT(*) FROM TEE_HotelCriteria_Criteria WHERE criteriaID = @id",
                        new SqlParameter("@id", id)).FirstOrDefault() > 0;
                if (inUse)
                {
                    res.message = "Δεν διαγράφεται: χρησιμοποιείται σε αξιολογήσεις. Χρησιμοποιήστε «Απόσυρση».";
                    return Ok(res);
                }

                Criteria c = unitOfWork.CriteriaRepository.GetByID(id);
                if (c != null) { unitOfWork.CriteriaRepository.Delete(c); unitOfWork.Save(); }
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); res.message = "Σφάλμα διαγραφής."; return Ok(res); }
        }

        // ── Τεκμήρια κριτηρίου (TEE_Criteria_Files) ─────────────────────
        [Route("api/AdminCriteriaApi/GetCriterionFiles")]
        [HttpPost]
        public IHttpActionResult GetCriterionFiles([FromBody] CriterionFileSaveRequest req)
        {
            var res = new CriterionFilesResult { success = false, files = new List<CriterionFileVM>() };
            try
            {
                if (!IsAdmin() || req == null || req.criteriaID <= 0) return Ok(res);

                decimal cid = req.criteriaID;
                var usedFileIds = new HashSet<decimal>(unitOfWork.context.Database
                    .SqlQuery<decimal>("SELECT DISTINCT criteriaFileID FROM TEE_HotelCriteria_CriteriaFiles"));

                res.files = unitOfWork.Criteria_FileRepository
                    .Get(x => x.criteriaID == cid)
                    .OrderBy(x => x.id)
                    .Select(f => new CriterionFileVM
                    {
                        id = f.id, criteriaID = f.criteriaID, title = f.title,
                        description = f.description, isActive = f.isActive,
                        inUse = usedFileIds.Contains(f.id)
                    }).ToList();

                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); return Ok(res); }
        }

        [Route("api/AdminCriteriaApi/SaveCriterionFile")]
        [HttpPost]
        public IHttpActionResult SaveCriterionFile([FromBody] CriterionFileSaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin()) { res.message = "Δεν επιτρέπεται."; return Ok(res); }
                if (req == null || req.criteriaID <= 0 || string.IsNullOrWhiteSpace(req.title))
                { res.message = "Συμπληρώστε τίτλο."; return Ok(res); }

                Criteria_File f;
                if (req.id.HasValue && req.id.Value > 0)
                {
                    f = unitOfWork.Criteria_FileRepository.GetByID(req.id.Value);
                    if (f == null) { res.message = "Δεν βρέθηκε."; return Ok(res); }
                }
                else
                {
                    f = new Criteria_File();
                    unitOfWork.Criteria_FileRepository.Insert(f);
                }

                f.criteriaID = req.criteriaID;
                f.title = req.title.Trim();
                f.description = req.description ?? "";   // NOT NULL στη ΒΔ
                f.isActive = req.isActive;

                unitOfWork.Save();
                res.success = true; res.id = f.id;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); res.message = "Σφάλμα αποθήκευσης."; return Ok(res); }
        }

        [Route("api/AdminCriteriaApi/ToggleCriterionFile")]
        [HttpPost]
        public IHttpActionResult ToggleCriterionFile([FromBody] CriterionFileSaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin() || req == null || !req.id.HasValue) return Ok(res);
                Criteria_File f = unitOfWork.Criteria_FileRepository.GetByID(req.id.Value);
                if (f == null) return Ok(res);
                f.isActive = req.isActive;
                unitOfWork.Criteria_FileRepository.Update(f);
                unitOfWork.Save();
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); return Ok(res); }
        }

        [Route("api/AdminCriteriaApi/DeleteCriterionFile")]
        [HttpPost]
        public IHttpActionResult DeleteCriterionFile([FromBody] CriterionFileSaveRequest req)
        {
            var res = new AdminCriteriaResult { success = false };
            try
            {
                if (!IsAdmin() || req == null || !req.id.HasValue) return Ok(res);
                decimal id = req.id.Value;

                bool inUse = unitOfWork.context.Database
                    .SqlQuery<int>("SELECT COUNT(*) FROM TEE_HotelCriteria_CriteriaFiles WHERE criteriaFileID = @id",
                        new SqlParameter("@id", id)).FirstOrDefault() > 0;
                if (inUse)
                {
                    res.message = "Δεν διαγράφεται: υπάρχουν μεταφορτωμένα τεκμήρια. Χρησιμοποιήστε «Απενεργοποίηση».";
                    return Ok(res);
                }

                Criteria_File f = unitOfWork.Criteria_FileRepository.GetByID(id);
                if (f != null) { unitOfWork.Criteria_FileRepository.Delete(f); unitOfWork.Save(); }
                res.success = true;
                return Ok(res);
            }
            catch (Exception exLog) { HotelsTEE.Utils.ErrorLogger.Log(exLog, "AdminCriteriaApiController.cs"); res.message = "Σφάλμα διαγραφής."; return Ok(res); }
        }
    }
}
