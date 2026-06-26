using HotelsTEE.DAL;
using HotelsTEE.Models;
using System.Collections.Generic;
using System.Linq;

namespace HotelsTEE.Utils
{
    // Server-side επιβολή των κανόνων κεντρικών ρυθμίσεων/παροχών.
    // Επιστρέφει τα criteriaID που πρέπει να είναι «δεν ισχύει» (isApplicable=false)
    // βάσει των απαντήσεων του συγκεκριμένου HotelCriteria (έκδοσης) — ώστε η
    // βαθμολογία να ΜΗΝ εξαρτάται από το τι στέλνει ο client.
    public static class FeatureRules
    {
        public static HashSet<decimal> GetFeatureDisabledCriteria(UnitOfWork uow, decimal hotelCriteriaID)
        {
            var result = new HashSet<decimal>();
            if (hotelCriteriaID <= 0) return result;

            List<HotelCriteriaFeature> answers = uow.HotelCriteriaFeatureRepository
                .Get(x => x.hotelCriteriaID == hotelCriteriaID).ToList();
            if (answers.Count == 0) return result;

            // Μόνο ενεργές ρυθμίσεις (όπως ακριβώς εμφανίζονται και στον client)
            var activeFeatureIds = new HashSet<decimal>(
                uow.PropertyFeatureRepository.Get(x => x.isActive).Select(f => f.featureID));

            var answerByFeature = answers
                .Where(a => activeFeatureIds.Contains(a.featureID))
                .GroupBy(a => a.featureID)
                .ToDictionary(g => g.Key, g => g.First().hasFeature);
            if (answerByFeature.Count == 0) return result;

            List<decimal> fIds = answerByFeature.Keys.ToList();
            List<FeatureCriteriaMap> maps = uow.FeatureCriteriaMapRepository
                .Get(x => fIds.Contains(x.featureID)).ToList();

            foreach (var m in maps)
            {
                bool present = answerByFeature[m.featureID];
                bool disable = m.disableWhenPresent ? present : !present;
                if (disable) result.Add(m.criteriaID);
            }

            return result;
        }
    }
}
