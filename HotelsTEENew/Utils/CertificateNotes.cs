using System;
using System.Linq;

namespace HotelsTEE.Utils
{
    // Το πεδίο HotelierCertificate.notes κρατά ιστορικό από δύο διαφορετικά
    // είδη απόρριψης (ανάθεσης από επιθεωρητή & τελικής κατάταξης από ξενοδόχο).
    // Εδώ κεντρικοποιούμε τη μορφή/αναγνώριση της απόρριψης ΤΕΛΙΚΗΣ ΚΑΤΑΤΑΞΗΣ,
    // ώστε να μην μπερδεύεται με την απόρριψη ανάθεσης.
    public static class CertificateNotes
    {
        public const string FinalRejectionPrefix = "Απόρριψη τελικής κατάταξης από ξενοδόχο:";

        // Μορφοποίηση καταχώρισης απόρριψης τελικής κατάταξης (με timestamp).
        public static string FormatFinalRejection(string comment)
        {
            return "[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "] "
                 + FinalRejectionPrefix + " " + (comment ?? "").Trim();
        }

        // Επιστρέφει ΜΟΝΟ τις γραμμές απόρριψης τελικής κατάταξης (ή null αν δεν υπάρχουν).
        public static string ExtractFinalRejection(string notes)
        {
            if (string.IsNullOrEmpty(notes)) return null;

            var lines = notes
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.IndexOf(FinalRejectionPrefix, StringComparison.Ordinal) >= 0)
                .ToList();

            return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : null;
        }

        public static bool HasFinalRejection(string notes)
        {
            return ExtractFinalRejection(notes) != null;
        }
    }
}
