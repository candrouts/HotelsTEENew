using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    // Επιλέξιμο κριτήριο για αντιστοίχιση (μόνο όσα έχουν notApplicable=1)
    public class EligibleCriteriaViewModel
    {
        public decimal id { get; set; }
        public string code { get; set; }
        public string title { get; set; }
        public string categoryTitle { get; set; }
    }

    public class FeatureMapItemViewModel
    {
        public decimal mapID { get; set; }
        public decimal criteriaID { get; set; }
        public string criteriaCode { get; set; }
        public string criteriaTitle { get; set; }
        public bool disableWhenPresent { get; set; }
    }

    public class FeatureItemViewModel
    {
        public decimal featureID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
        public int displayOrder { get; set; }
        public bool isActive { get; set; }
        public List<FeatureMapItemViewModel> mappings { get; set; }
    }

    public class AdminFeaturesDataViewModel
    {
        public bool success { get; set; }
        public List<FeatureItemViewModel> features { get; set; }
        public List<EligibleCriteriaViewModel> criteria { get; set; }
    }

    // Αιτήματα αποθήκευσης
    public class FeatureSaveRequest
    {
        public decimal? featureID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
        public int displayOrder { get; set; }
        public bool isActive { get; set; }
    }

    public class MappingSaveRequest
    {
        public decimal? mapID { get; set; }
        public decimal featureID { get; set; }
        public decimal criteriaID { get; set; }
        public bool disableWhenPresent { get; set; }
    }

    public class FeatureApiResult
    {
        public bool success { get; set; }
        public decimal id { get; set; }
        public string message { get; set; }
    }

    // ── Client-side (σελίδα κριτηρίων ξενοδόχου/επιθεωρητή) ──────────
    public class PropertyFeatureClientViewModel
    {
        public decimal featureID { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
    }

    public class FeatureMapClientViewModel
    {
        public decimal featureID { get; set; }
        public decimal criteriaID { get; set; }
        public bool disableWhenPresent { get; set; }
    }

    public class FeatureAnswerViewModel
    {
        public decimal featureID { get; set; }
        public bool hasFeature { get; set; }
    }

    public class FeatureAnswerSaveRequest
    {
        public decimal hotelCriteriaID { get; set; }
        public decimal featureID { get; set; }
        public bool hasFeature { get; set; }
    }
}
