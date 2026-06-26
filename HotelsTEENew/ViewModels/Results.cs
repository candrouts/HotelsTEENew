using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HotelsTEE.ViewModels
{
    public class Results
    {

        public List<CriteriaViewModel> criteria { get; set; }

        public List<CategoryViewModel> categories { get; set; }

        public HotelDetailsViewModel hotelDetails { get; set; }


        public List<MedalViewModel> medals { get; set; }

        public HotelCriteriaViewModel hotelCriteria { get; set; }

        public List<CertificateViewModel> certificates { get; set; }

        public CertificateViewModel certificate { get; set; }

        public UserViewModel user { get; set; }


        public List<HotelCriteria_CriteriaFileViewModel> hotelCriteria_criteriaFiles { get; set; }

        // Per-pillar βάσεις μεταλλίων + διακόπτης (για το gating στη μπάρα)
        public bool usePillarThresholds { get; set; }
        public List<ThresholdCellViewModel> medalThresholds { get; set; }

        // True όταν δεν υπάρχει ενεργή αυτοαξιολόγηση και θα δημιουργούνταν νέα:
        // δεν δημιουργούμε αυτόματα — ζητάμε επιβεβαίωση από τον χρήστη.
        public bool needsNewAssessment { get; set; }

        // Κεντρικές ρυθμίσεις/παροχές καταλύματος + αντιστοιχίσεις + απαντήσεις κύκλου
        public List<PropertyFeatureClientViewModel> features { get; set; }
        public List<FeatureMapClientViewModel> featureMaps { get; set; }
        public List<FeatureAnswerViewModel> featureAnswers { get; set; }

    }
}