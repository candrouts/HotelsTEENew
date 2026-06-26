using HotelsTEE.DAL;
using HotelsTEE.Models;
using HotelsTEE.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace HotelsTEE.Utils
{
    public static class ScoringHelper
    {
        // Διαβάζει boolean ρύθμιση από τον πίνακα TEE_Settings
        public static bool GetBoolSetting(UnitOfWork uow, string key, bool defaultValue = false)
        {
            AppSetting s = uow.SettingRepository.Get(x => x.settingKey == key).FirstOrDefault();
            if (s == null || string.IsNullOrEmpty(s.settingValue)) return defaultValue;
            return s.settingValue == "1" || s.settingValue.ToLower() == "true";
        }

        // Υπολογίζει αναγμένες βαθμολογίες ανά κύριο πυλώνα, εφαρμόζει το (gated) μετάλιο
        // και ενημερώνει το hotelCriteria.medalID + hotelCriteria.totalScore.
        public static MedalEvalResult ApplyMedal(UnitOfWork uow, HotelCriteria hotelCriteria,
                                                 List<HotelCriteria_Criteria> savedCriteria)
        {
            // Δομή κατηγοριών
            List<CategoryViewModel> cats = uow.context.Database
                .SqlQuery<CategoryViewModel>("Select * from V_TEE_Categories where isActive=1").ToList();

            Dictionary<decimal, decimal> subToMain = cats.Where(c => c.parentID.HasValue)
                .ToDictionary(c => c.id, c => c.parentID.Value);
            Dictionary<decimal, decimal> pillarMax = cats.Where(c => !c.parentID.HasValue)
                .ToDictionary(c => c.id, c => c.totalUnits ?? 0);

            // Ορισμοί κριτηρίων (για max & τύπο)
            List<decimal> critIds = savedCriteria.Select(x => x.criteriaID).Distinct().ToList();
            Dictionary<decimal, Criteria> defs = uow.CriteriaRepository
                .Get(c => critIds.Contains(c.id)).ToList()
                .GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First());

            var raw = new Dictionary<decimal, decimal>();
            var rawMax = new Dictionary<decimal, decimal>();

            foreach (var sc in savedCriteria)
            {
                if (!defs.ContainsKey(sc.criteriaID)) continue;
                Criteria def = defs[sc.criteriaID];
                if (!subToMain.ContainsKey(def.categoryID)) continue;
                decimal mp = subToMain[def.categoryID];

                if (!raw.ContainsKey(mp)) { raw[mp] = 0; rawMax[mp] = 0; }

                raw[mp] += sc.points ?? 0;

                // max ανά κριτήριο — ίδιοι κανόνες με τον client (JS maxGrade)
                decimal cmax = 0;
                if (sc.isApplicable)
                {
                    if (def.criteriaType == 1 || def.criteriaType == 2)
                        cmax = def.weight * def.maxGrade;
                    else if (def.criteriaType == 3 && sc.isChecked == true)
                        cmax = def.weight * def.maxGrade;
                }
                rawMax[mp] += cmax;
            }

            // Αναγωγή ανά πυλώνα + αναγμένο σύνολο
            var pillarScores = new Dictionary<decimal, decimal>();
            decimal normalizedTotal = 0;
            foreach (var mp in pillarMax.Keys)
            {
                decimal r = raw.ContainsKey(mp) ? raw[mp] : 0;
                decimal rm = rawMax.ContainsKey(mp) ? rawMax[mp] : 0;
                decimal norm = rm > 0 ? (r / rm) * pillarMax[mp] : 0;
                pillarScores[mp] = norm;
                normalizedTotal += norm;
            }

            // Μετάλια + βάσεις + διακόπτης
            List<Medal> medals = uow.MedalRepository.Get().ToList();
            List<MedalPillarThreshold> thresholds = uow.MedalPillarThresholdRepository.Get().ToList();
            bool usePillar = GetBoolSetting(uow, "medal.usePillarThresholds", false);

            MedalEvalResult result = MedalEvaluator.Evaluate(
                normalizedTotal, pillarScores, pillarMax, medals, thresholds, usePillar);

            hotelCriteria.medalID = result.awardedMedalID;
            hotelCriteria.totalScore = decimal.Round(normalizedTotal, 2);

            return result;
        }
    }
}
