using HotelsTEE.DAL;
using HotelsTEE.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace HotelsTEE.Utils
{
    // Server-side κανόνες κριτηρίων βάσει δυναμικότητας καταλύματος (κλίνες).
    // Ίδιοι κανόνες με τον client (CriteriaViewModel / CertificateViewModel), ώστε
    // βαθμολογία και υποχρεωτικότητα να μην εξαρτώνται από το τι στέλνει ο browser.
    public static class CapacityRules
    {
        private class Rule
        {
            public string Code;
            public Func<int, bool> When;
            public Rule(string code, Func<int, bool> when) { Code = code; When = when; }
        }

        // Υποχρεωτικά (πρέπει να καλύπτονται) όταν ισχύει η συνθήκη κλινών
        private static readonly Rule[] RequiredRules =
        {
            new Rule("ΔΑ_ΣΑ_2", beds => beds > 100),   // χωριστή συλλογή βιοαποβλήτων
            new Rule("ΑΔ_ΠΔ_1", beds => beds >= 51),   // πιστοποιητικό πυρασφάλειας (νομική υποχρέωση)
        };

        // «Δεν εφαρμόζεται» όταν ισχύει η συνθήκη κλινών
        private static readonly Rule[] NotApplicableRules =
        {
            new Rule("ΔΑ_ΣΑ_1", beds => beds > 100),   // διαλογή 4 ρευμάτων: νομική υποχρέωση > 100
        };

        // Πολλαπλασιαστής βαρύτητας βάσει δωματίων (Οδηγός Εφαρμογής — Water Metering:
        // σε μονάδες > 40 δωματίων τα υποκριτήρια αυτά έχουν διπλή βαρύτητα)
        private const decimal DoubleWeightFactor = 2m;
        private static readonly Rule[] DoubleWeightRules =
        {
            new Rule("ΔΥ_WM_3", rooms => rooms > 40),
            new Rule("ΔΥ_WM_4", rooms => rooms > 40),
            new Rule("ΔΥ_WM_5", rooms => rooms > 40),
        };

        public static int GetTotalRooms(UnitOfWork uow, object hotelID, object companyID)
        {
            int? rooms = uow.context.Database.SqlQuery<int?>(
                "SELECT TOP 1 CAST(totalRooms AS INT) FROM V_TEE_HotelDetails WHERE hotelID = @hotelID AND exploitingCompanyID = @companyID",
                new SqlParameter("@hotelID", hotelID ?? DBNull.Value),
                new SqlParameter("@companyID", companyID ?? DBNull.Value)).FirstOrDefault();
            return rooms ?? 0;
        }

        // criteriaID -> πολλαπλασιαστής βάρους (μόνο όσα διαφέρουν από 1)
        public static Dictionary<decimal, decimal> GetWeightFactors(UnitOfWork uow, object hotelID, object companyID)
        {
            var result = new Dictionary<decimal, decimal>();
            foreach (decimal id in Resolve(uow, DoubleWeightRules, GetTotalRooms(uow, hotelID, companyID)))
                result[id] = DoubleWeightFactor;
            return result;
        }

        public static decimal Factor(Dictionary<decimal, decimal> factors, decimal criteriaID)
        {
            decimal f;
            return factors != null && factors.TryGetValue(criteriaID, out f) ? f : 1m;
        }

        public static int GetTotalBeds(UnitOfWork uow, object hotelID, object companyID)
        {
            int? beds = uow.context.Database.SqlQuery<int?>(
                "SELECT TOP 1 CAST(totalBeds AS INT) FROM V_TEE_HotelDetails WHERE hotelID = @hotelID AND exploitingCompanyID = @companyID",
                new SqlParameter("@hotelID", hotelID ?? DBNull.Value),
                new SqlParameter("@companyID", companyID ?? DBNull.Value)).FirstOrDefault();
            return beds ?? 0;
        }

        private static HashSet<decimal> Resolve(UnitOfWork uow, Rule[] rules, int beds)
        {
            var result = new HashSet<decimal>();
            List<string> codes = rules.Where(r => r.When(beds)).Select(r => r.Code).ToList();
            if (codes.Count == 0) return result;

            foreach (decimal id in uow.CriteriaRepository.Get(x => codes.Contains(x.code)).Select(c => c.id))
                result.Add(id);
            return result;
        }

        public static HashSet<decimal> GetCapacityRequiredCriteria(UnitOfWork uow, object hotelID, object companyID)
        {
            return Resolve(uow, RequiredRules, GetTotalBeds(uow, hotelID, companyID));
        }

        public static HashSet<decimal> GetCapacityNotApplicableCriteria(UnitOfWork uow, object hotelID, object companyID)
        {
            return Resolve(uow, NotApplicableRules, GetTotalBeds(uow, hotelID, companyID));
        }

        // Κανόνας ζεύγους αποχέτευσης (Οδηγός Εφαρμογής): υποχρεωτικά ένα από τα δύο «Ναι».
        // Σύνδεση σε δίκτυο (ΔΑ_ΥΑ_1) «Ναι» => η επιτόπια επεξεργασία (ΔΑ_ΥΑ_2) είναι προαιρετική·
        // αλλιώς (Όχι / Δ/Α) η ΔΑ_ΥΑ_2 είναι υποχρεωτική «Ναι». Ίδιος κανόνας και στον client.
        public const string SewerConnectionCode = "ΔΑ_ΥΑ_1";
        public const string OnSiteTreatmentCode = "ΔΑ_ΥΑ_2";

        // Επιστρέφει μήνυμα σφάλματος ή null αν ο κανόνας ικανοποιείται
        public static string CheckSewageRule(UnitOfWork uow, IEnumerable<HotelsTEE.ViewModels.HotelCriteria_CriteriaViewModel> answers)
        {
            var codes = new[] { SewerConnectionCode, OnSiteTreatmentCode };
            var ids = uow.CriteriaRepository.Get(x => codes.Contains(x.code)).ToDictionary(c => c.code, c => c.id);
            if (ids.Count < 2 || answers == null) return null;

            Func<string, bool> yes = code =>
            {
                var a = answers.FirstOrDefault(c => c.criteriaID == ids[code]);
                return a != null && a.isApplicable && a.isChecked == true;
            };

            if (yes(SewerConnectionCode) || yes(OnSiteTreatmentCode)) return null;
            return "Πρέπει να τεκμηριωθεί είτε σύνδεση σε δίκτυο αποχέτευσης (" + SewerConnectionCode +
                   ") είτε επεξεργασία λυμάτων εντός εγκατάστασης (" + OnSiteTreatmentCode + ").";
        }

        // Ίδια σημασιολογία με το isValidRequired του client:
        // Ναι/Όχι => πρέπει «Ναι»· λίστα τιμών => επιλεγμένο και όχι 0.
        public static bool IsSatisfied(Criteria crit, bool? isChecked, string value)
        {
            if (crit.criteriaType == 1 || crit.criteriaType == 3)
                return isChecked == true;

            if (crit.criteriaType == 2)
            {
                if (string.IsNullOrWhiteSpace(value)) return false;
                decimal v;
                return decimal.TryParse(value.Replace(".", ","), out v) && v != 0;
            }

            return true;
        }
    }
}
