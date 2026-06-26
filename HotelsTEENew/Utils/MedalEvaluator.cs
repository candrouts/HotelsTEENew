using HotelsTEE.Models;
using System.Collections.Generic;
using System.Linq;

namespace HotelsTEE.Utils
{
    public class MedalEvalResult
    {
        public decimal? awardedMedalID { get; set; }      // το μετάλιο που απονέμεται (μετά το gating)
        public decimal? byTotalMedalID { get; set; }      // το μετάλιο βάσει μόνο της συνολικής βαθμολογίας
        public decimal? blockedPillarCategoryID { get; set; } // πυλώνας που εμπόδισε το μετάλιο-βάσει-συνόλου
        public decimal normalizedTotal { get; set; }
    }

    public static class MedalEvaluator
    {
        // Απαιτούμενη βάση πυλώνα για ένα threshold (στην αναγμένη κλίμακα 0..totalUnits)
        public static decimal RequiredValue(MedalPillarThreshold t, decimal pillarMax)
        {
            return t.isPercent ? (t.minValue / 100m) * pillarMax : t.minValue;
        }

        public static MedalEvalResult Evaluate(
            decimal normalizedTotal,
            Dictionary<decimal, decimal> pillarScores,   // mainPillarID -> αναγμένη βαθμολογία
            Dictionary<decimal, decimal> pillarMax,      // mainPillarID -> totalUnits (max)
            List<Medal> medals,
            List<MedalPillarThreshold> thresholds,
            bool usePillarThresholds)
        {
            var result = new MedalEvalResult { normalizedTotal = normalizedTotal };

            if (medals == null || medals.Count == 0)
                return result;

            // Υποψήφια μετάλια: όσα καλύπτονται από τη συνολική βαθμολογία, από το υψηλότερο προς το χαμηλότερο
            var candidates = medals.Where(m => normalizedTotal >= m.min)
                                   .OrderByDescending(m => m.min)
                                   .ToList();

            Medal byTotal = candidates.FirstOrDefault();
            result.byTotalMedalID = byTotal?.id;
            result.awardedMedalID = byTotal?.id;

            if (!usePillarThresholds || byTotal == null)
                return result;

            // Από το μετάλιο-βάσει-συνόλου προς τα κάτω, βρες το πρώτο που περνάει όλες τις βάσεις πυλώνων
            foreach (var m in candidates)
            {
                var medalThresholds = thresholds.Where(t => t.medalID == m.id).ToList();
                bool allOk = true;

                foreach (var t in medalThresholds)
                {
                    decimal pmax = pillarMax.ContainsKey(t.categoryID) ? pillarMax[t.categoryID] : 0;
                    decimal required = RequiredValue(t, pmax);
                    decimal pscore = pillarScores.ContainsKey(t.categoryID) ? pillarScores[t.categoryID] : 0;
                    if (pscore < required) { allOk = false; break; }
                }

                if (allOk)
                {
                    result.awardedMedalID = m.id;
                    // Αν υποβιβαστήκαμε, βρες ποιος πυλώνας μπλόκαρε το μετάλιο-βάσει-συνόλου (για μήνυμα)
                    if (m.id != byTotal.id)
                        result.blockedPillarCategoryID = FirstBlockingPillar(byTotal, thresholds, pillarMax, pillarScores);
                    return result;
                }
            }

            return result;
        }

        private static decimal? FirstBlockingPillar(
            Medal medal,
            List<MedalPillarThreshold> thresholds,
            Dictionary<decimal, decimal> pillarMax,
            Dictionary<decimal, decimal> pillarScores)
        {
            foreach (var t in thresholds.Where(x => x.medalID == medal.id))
            {
                decimal pmax = pillarMax.ContainsKey(t.categoryID) ? pillarMax[t.categoryID] : 0;
                decimal required = RequiredValue(t, pmax);
                decimal pscore = pillarScores.ContainsKey(t.categoryID) ? pillarScores[t.categoryID] : 0;
                if (pscore < required) return t.categoryID;
            }
            return null;
        }
    }
}
