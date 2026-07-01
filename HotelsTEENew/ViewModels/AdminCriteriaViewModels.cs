using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    // ── Δέντρο διαχείρισης: Πυλώνες → Υποπυλώνες → Κριτήρια ──────────
    public class AdminCriterionVM
    {
        public decimal id { get; set; }
        public decimal categoryID { get; set; }
        public string code { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int order { get; set; }
        public int weight { get; set; }
        public decimal? maxGrade { get; set; }
        public int criteriaType { get; set; }
        public string gradesList { get; set; }
        public string gradesOptions { get; set; }
        public string selectList { get; set; }
        public string notes1 { get; set; }
        public string notes2 { get; set; }
        public bool needsFiles { get; set; }
        public bool notApplicable { get; set; }
        public bool isRequired { get; set; }
        public string dateFrom { get; set; }   // yyyy-MM-dd
        public string dateTo { get; set; }      // yyyy-MM-dd
        public bool isActiveNow { get; set; }   // dateFrom <= σήμερα <= dateTo
        public bool inUse { get; set; }         // υπάρχει σε αξιολογήσεις (HotelCriteria_Criteria)
        public int filesCount { get; set; }     // πλήθος απαιτούμενων τεκμηρίων
    }

    // ── Τεκμήρια κριτηρίου (TEE_Criteria_Files) ─────────────────────
    public class CriterionFileVM
    {
        public decimal id { get; set; }
        public decimal criteriaID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public bool isActive { get; set; }
        public bool inUse { get; set; }   // υπάρχει μεταφορτωμένο τεκμήριο σε αξιολόγηση
    }

    public class CriterionFileSaveRequest
    {
        public decimal? id { get; set; }
        public decimal criteriaID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public bool isActive { get; set; }
    }

    public class CriterionFilesResult
    {
        public bool success { get; set; }
        public List<CriterionFileVM> files { get; set; }
    }

    public class AdminSubPillarVM
    {
        public decimal id { get; set; }
        public decimal? parentID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string examples { get; set; }
        public int order { get; set; }
        public decimal? totalUnits { get; set; }
        public decimal? maxGrade { get; set; }
        public bool isActive { get; set; }
        public List<AdminCriterionVM> criteria { get; set; }
        public bool canDelete { get; set; }    // δεν έχει κριτήρια
    }

    public class AdminPillarVM
    {
        public decimal id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string examples { get; set; }
        public int order { get; set; }
        public decimal? totalUnits { get; set; }
        public decimal? maxGrade { get; set; }
        public bool isActive { get; set; }
        public List<AdminSubPillarVM> subPillars { get; set; }
        public bool canDelete { get; set; }    // δεν έχει υποπυλώνες
    }

    public class AdminCriteriaTreeVM
    {
        public bool success { get; set; }
        public List<AdminPillarVM> pillars { get; set; }
    }

    // ── Αιτήματα αποθήκευσης ────────────────────────────────────────
    public class CategorySaveRequest
    {
        public decimal? id { get; set; }
        public decimal? parentID { get; set; }   // null = πυλώνας, τιμή = υποπυλώνας
        public string title { get; set; }
        public string description { get; set; }
        public string examples { get; set; }
        public int order { get; set; }
        public decimal? totalUnits { get; set; }
        public decimal? maxGrade { get; set; }
        public bool isActive { get; set; }
    }

    public class CriterionSaveRequest
    {
        public decimal? id { get; set; }
        public decimal categoryID { get; set; }
        public string code { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public int order { get; set; }
        public int weight { get; set; }
        public decimal? maxGrade { get; set; }
        public int criteriaType { get; set; }
        public string gradesList { get; set; }
        public string gradesOptions { get; set; }
        public string selectList { get; set; }
        public string notes1 { get; set; }
        public string notes2 { get; set; }
        public bool needsFiles { get; set; }
        public bool notApplicable { get; set; }
        public bool isRequired { get; set; }
        public string dateFrom { get; set; }
        public string dateTo { get; set; }
        public bool active { get; set; }   // για SetCriterionActive
    }

    public class AdminCriteriaResult
    {
        public bool success { get; set; }
        public decimal id { get; set; }
        public string message { get; set; }
    }
}
