using HotelsTEE.DAL;
using HotelsTEE.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace HotelsTEE.Utils
{
    // Server-side υποχρεωτικότητα κριτηρίων βάσει δυναμικότητας καταλύματος (κλίνες).
    // Ίδιος κανόνας με τον client (CriteriaViewModel / CertificateViewModel), ώστε
    // η υποχρεωτικότητα να μην εξαρτάται από το τι στέλνει ο browser.
    public static class CapacityRules
    {
        // Υποχρεωτικό για μονάδες ΑΝΩ των 100 κλινών (αυστηρά > 100)
        public const int BedsThreshold = 100;

        // Κριτήρια που γίνονται υποχρεωτικά πάνω από το όριο κλινών
        private static readonly string[] Codes = { "ΔΑ_ΣΑ_2" };

        // Κριτήρια που ΔΕΝ εφαρμόζονται πάνω από το όριο κλινών (π.χ. η διαλογή
        // 4 ρευμάτων είναι νομική υποχρέωση για μονάδες > 100 κλινών)
        private static readonly string[] NotApplicableCodes = { "ΔΑ_ΣΑ_1" };

        public static int GetTotalBeds(UnitOfWork uow, object hotelID, object companyID)
        {
            int? beds = uow.context.Database.SqlQuery<int?>(
                "SELECT TOP 1 CAST(totalBeds AS INT) FROM V_TEE_HotelDetails WHERE hotelID = @hotelID AND exploitingCompanyID = @companyID",
                new SqlParameter("@hotelID", hotelID ?? DBNull.Value),
                new SqlParameter("@companyID", companyID ?? DBNull.Value)).FirstOrDefault();
            return beds ?? 0;
        }

        // criteriaID που είναι υποχρεωτικά για το συγκεκριμένο κατάλυμα λόγω δυναμικότητας
        public static HashSet<decimal> GetCapacityRequiredCriteria(UnitOfWork uow, object hotelID, object companyID)
        {
            var result = new HashSet<decimal>();
            if (GetTotalBeds(uow, hotelID, companyID) <= BedsThreshold)
                return result;

            foreach (decimal id in uow.CriteriaRepository.Get(x => Codes.Contains(x.code)).Select(c => c.id))
                result.Add(id);
            return result;
        }

        // criteriaID που είναι «δεν εφαρμόζεται» για το κατάλυμα λόγω δυναμικότητας
        public static HashSet<decimal> GetCapacityNotApplicableCriteria(UnitOfWork uow, object hotelID, object companyID)
        {
            var result = new HashSet<decimal>();
            if (GetTotalBeds(uow, hotelID, companyID) <= BedsThreshold)
                return result;

            foreach (decimal id in uow.CriteriaRepository.Get(x => NotApplicableCodes.Contains(x.code)).Select(c => c.id))
                result.Add(id);
            return result;
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
