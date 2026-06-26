using System;
using System.Collections.Generic;

namespace HotelsTEE.ViewModels
{
    // Στοιχείο για το admin UI: catalog event + το (τυχόν) template του
    public class NotificationEventRowViewModel
    {
        public string eventKey { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public List<string> tokens { get; set; }

        public bool isActive { get; set; }
        public int recipientType { get; set; }
        public string customEmail { get; set; }
        public string subject { get; set; }
        public string body { get; set; }
    }

    // Payload αποθήκευσης template από admin
    public class NotificationTemplateSaveViewModel
    {
        public string eventKey { get; set; }
        public bool isActive { get; set; }
        public int recipientType { get; set; }
        public string customEmail { get; set; }
        public string subject { get; set; }
        public string body { get; set; }
    }

    public class NotificationLogRowViewModel
    {
        public int id { get; set; }
        public string eventKey { get; set; }
        public string toEmail { get; set; }
        public string subject { get; set; }
        public DateTime sentDateTime { get; set; }
        public bool success { get; set; }
        public string error { get; set; }
    }
}
