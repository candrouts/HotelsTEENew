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

        // Κριτήρια που βαθμολογούνται ΠΑΝΤΑ ως Ναι/Όχι (τύπος 1: μετρούν στο μέγιστο
        // όταν εφαρμόζονται), ανεξάρτητα από τον τύπο στη βάση. Για < 51 κλίνες το
        // ΑΔ_ΠΔ_1 είναι προαιρετικό: ο χρήστης το σημειώνει «Δεν εφαρμόζεται» (Δ/Α)
        // με τον διακόπτη — η επιλογή του σεβαστή και στον server.
        private static readonly string[] YesNoCodes = { "ΑΔ_ΠΔ_1" };

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

        // Αποτελεσματικός τύπος για τη βαθμολόγηση (ίδιος με τον client).
        // ΔΕΝ αλλάζει το entity (tracked από το EF) — μόνο υπολογισμός.
        public static int EffectiveType(Criteria crit)
        {
            if (crit.criteriaType == 3 && YesNoCodes.Contains(crit.code))
                return 1;
            return crit.criteriaType;
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
