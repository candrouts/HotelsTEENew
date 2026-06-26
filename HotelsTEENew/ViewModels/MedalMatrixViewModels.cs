using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    public class MedalRowViewModel
    {
        public decimal id { get; set; }
        public string title { get; set; }
        public decimal min { get; set; }
        public decimal max { get; set; }
    }

    public class PillarRowViewModel
    {
        public decimal id { get; set; }
        public string title { get; set; }
        public decimal totalUnits { get; set; }
    }

    public class ThresholdCellViewModel
    {
        public decimal medalID { get; set; }
        public decimal categoryID { get; set; }
        public decimal minValue { get; set; }
        public bool isPercent { get; set; }
    }

    // Payload αποθήκευσης μήτρας
    public class MedalMatrixSaveViewModel
    {
        public bool usePillarThresholds { get; set; }
        public List<ThresholdCellViewModel> cells { get; set; }
    }
}
