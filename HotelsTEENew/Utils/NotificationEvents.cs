using System.Collections.Generic;

namespace HotelsTEE.Utils
{
    // Κατάλογος γεγονότων ειδοποίησης — ορίζεται στον κώδικα (όχι από admin).
    // Ο admin επεξεργάζεται μόνο τα templates (κείμενο/παραλήπτη/on-off) ανά γεγονός.
    public class NotificationEventDef
    {
        public string key { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string[] tokens { get; set; }      // διαθέσιμα placeholders
        public int defaultRecipient { get; set; }  // 1=Ξενοδόχος, 2=Επιθεωρητής, 3=Admin
        public string defaultSubject { get; set; }
        public string defaultBody { get; set; }
    }

    public static class NotificationEvents
    {
        // Κοινά tokens διαθέσιμα σε όλα τα γεγονότα: {hotelName}, {link}

        public static readonly List<NotificationEventDef> All = new List<NotificationEventDef>
        {
            new NotificationEventDef {
                key = "ASSIGNMENT_SUBMITTED",
                title = "Υποβολή αίτησης ανάθεσης",
                description = "Ο ξενοδόχος υπέβαλε αίτηση και επέλεξε επιθεωρητή.",
                tokens = new[] { "{hotelName}", "{inspectorName}", "{proposedDate}", "{link}" },
                defaultRecipient = 2,
                defaultSubject = "Νέα ανάθεση αξιολόγησης — {hotelName}",
                defaultBody = "Σας ανατέθηκε η αξιολόγηση του καταλύματος <strong>{hotelName}</strong>.<br/>Προτεινόμενη ημερομηνία επιθεώρησης: <strong>{proposedDate}</strong>.<br/><br/>Συνδεθείτε στην πλατφόρμα για να επιβεβαιώσετε ή να αλλάξετε την ημερομηνία: {link}"
            },
            new NotificationEventDef {
                key = "AUTOPSY_DATE_ACCEPTED",
                title = "Αποδοχή ημερομηνίας αυτοψίας",
                description = "Ο επιθεωρητής αποδέχθηκε την προτεινόμενη ημερομηνία.",
                tokens = new[] { "{hotelName}", "{inspectorName}", "{date}", "{link}" },
                defaultRecipient = 1,
                defaultSubject = "Επιβεβαίωση ημερομηνίας αυτοψίας — {hotelName}",
                defaultBody = "Ο επιθεωρητής <strong>{inspectorName}</strong> αποδέχθηκε την ημερομηνία αυτοψίας <strong>{date}</strong> για το κατάλυμα {hotelName}.<br/><br/>{link}"
            },
            new NotificationEventDef {
                key = "AUTOPSY_DATE_CHANGED",
                title = "Αλλαγή ημερομηνίας αυτοψίας",
                description = "Ο επιθεωρητής όρισε νέα ημερομηνία αυτοψίας.",
                tokens = new[] { "{hotelName}", "{inspectorName}", "{date}", "{link}" },
                defaultRecipient = 1,
                defaultSubject = "Νέα ημερομηνία αυτοψίας — {hotelName}",
                defaultBody = "Ο επιθεωρητής <strong>{inspectorName}</strong> όρισε νέα ημερομηνία αυτοψίας: <strong>{date}</strong> για το κατάλυμα {hotelName}.<br/><br/>{link}"
            },
            new NotificationEventDef {
                key = "ASSIGNMENT_REJECTED",
                title = "Απόρριψη ανάθεσης από επιθεωρητή",
                description = "Ο επιθεωρητής απέρριψε την ανάθεση.",
                tokens = new[] { "{hotelName}", "{inspectorName}", "{comment}", "{link}" },
                defaultRecipient = 1,
                defaultSubject = "Απόρριψη ανάθεσης — {hotelName}",
                defaultBody = "Ο επιθεωρητής <strong>{inspectorName}</strong> απέρριψε την ανάθεση για το κατάλυμα {hotelName}.<br/>Παρατηρήσεις: {comment}<br/><br/>Παρακαλώ επιλέξτε νέο επιθεωρητή: {link}"
            },
            new NotificationEventDef {
                key = "FINAL_SUBMITTED",
                title = "Υποβολή τελικής κατάταξης",
                description = "Ο επιθεωρητής ολοκλήρωσε και υπέβαλε την τελική κατάταξη.",
                tokens = new[] { "{hotelName}", "{inspectorName}", "{points}", "{medal}", "{link}" },
                defaultRecipient = 1,
                defaultSubject = "Τελική κατάταξη προς αποδοχή — {hotelName}",
                defaultBody = "Ο επιθεωρητής ολοκλήρωσε την τελική κατάταξη του καταλύματος {hotelName}.<br/>Βαθμολογία: <strong>{points}</strong> — Μετάλλιο: <strong>{medal}</strong>.<br/><br/>Απαιτείται η αποδοχή σας για την έκδοση της βεβαίωσης: {link}"
            },
            new NotificationEventDef {
                key = "FINAL_REJECTED",
                title = "Απόρριψη τελικής κατάταξης από ξενοδόχο",
                description = "Ο ξενοδόχος απέρριψε την τελική κατάταξη.",
                tokens = new[] { "{hotelName}", "{comment}", "{link}" },
                defaultRecipient = 2,
                defaultSubject = "Απόρριψη τελικής κατάταξης — {hotelName}",
                defaultBody = "Ο ξενοδόχος απέρριψε την τελική κατάταξη του καταλύματος {hotelName}.<br/>Παρατηρήσεις: {comment}<br/><br/>Παρακαλώ υποβάλετε διορθωμένη τελική κατάταξη: {link}"
            },
            new NotificationEventDef {
                key = "CERTIFICATE_ISSUED",
                title = "Έκδοση βεβαίωσης",
                description = "Ο ξενοδόχος αποδέχθηκε την τελική κατάταξη και εκδόθηκε η βεβαίωση.",
                tokens = new[] { "{hotelName}", "{validUntil}", "{link}" },
                defaultRecipient = 1,
                defaultSubject = "Έκδοση βεβαίωσης βιωσιμότητας — {hotelName}",
                defaultBody = "Η βεβαίωση βιωσιμότητας για το κατάλυμα {hotelName} εκδόθηκε και ισχύει έως <strong>{validUntil}</strong>.<br/><br/>{link}"
            }
        };

        public static NotificationEventDef Get(string key)
        {
            return All.Find(x => x.key == key);
        }
    }
}
