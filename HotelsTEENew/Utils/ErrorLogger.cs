using System;
using System.Configuration;
using System.Data.SqlClient;

namespace HotelsTEE.Utils
{
    // Κεντρική καταγραφή σφαλμάτων στο TEE_ErrorLog.
    // Δικό του SqlConnection (όχι EF) ώστε να γράφει ακόμα κι αν το context
    // βρίσκεται σε προβληματική κατάσταση. ΔΕΝ πετάει ποτέ exception.
    public static class ErrorLogger
    {
        public static void Log(Exception ex, string source, string userName = null)
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["HotelsTEEContext"].ConnectionString;
                using (var conn = new SqlConnection(cs))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO TEE_ErrorLog (source, userName, message, stackTrace)
                                        VALUES (@source, @user, @msg, @stack)";
                    cmd.Parameters.AddWithValue("@source", (object)Truncate(source, 300) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@user", (object)Truncate(userName, 256) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@msg", (object)Truncate(Flatten(ex), 2000) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@stack", (object)(ex != null ? ex.ToString() : null) ?? DBNull.Value);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception)
            {
                // Η καταγραφή δεν πρέπει ποτέ να ρίξει την εφαρμογή.
            }
        }

        // Μήνυμα + inner exceptions σε μία γραμμή
        private static string Flatten(Exception ex)
        {
            if (ex == null) return null;
            string msg = ex.Message;
            Exception inner = ex.InnerException;
            int depth = 0;
            while (inner != null && depth++ < 5)
            {
                msg += " → " + inner.Message;
                inner = inner.InnerException;
            }
            return msg;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
