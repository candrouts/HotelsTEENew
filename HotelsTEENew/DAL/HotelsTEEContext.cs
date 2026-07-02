using HotelsTEE.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace HotelsTEE.DAL
{
    public class HotelsTEEContext : DbContext
    {
        // Database-first βάση (χωρίς Code First Migrations): απενεργοποιούμε τον
        // initializer ώστε το EF να μην ψάχνει τον πίνακα __MigrationHistory
        // (error 208 στο App Insights σε κάθε app start).
        static HotelsTEEContext()
        {
            Database.SetInitializer<HotelsTEEContext>(null);
        }

        public DbSet<CriteriaCategory> CriteriaCategories { get; set; }
        public DbSet<Criteria> Criteria { get; set; }

        public DbSet<Criteria_File> Criteria_Files { get; set; }


        public DbSet<HotelCriteria> HotelCriteria { get; set; }
        public DbSet<HotelCriteria_Criteria> HotelCriteria_Criteria { get; set; }

        public DbSet<Medal> Medals { get; set; }

        public DbSet<Inspector> Inspectors { get; set; }

        public DbSet<HotelierCertificate> HotelierCertificates { get; set; }

        public DbSet<HotelCriteria_CriteriaFile> HotelCriteria_CriteriaFiles { get; set; }

        public DbSet<InspectorArea> InspectorAreas { get; set; }

        public DbSet<NotificationTemplate> NotificationTemplates { get; set; }

        public DbSet<NotificationLog> NotificationLogs { get; set; }

        public DbSet<MedalPillarThreshold> MedalPillarThresholds { get; set; }

        public DbSet<AppSetting> Settings { get; set; }

        public DbSet<CertificateFile> CertificateFiles { get; set; }

        public DbSet<CertificateTemplate> CertificateTemplates { get; set; }

        public DbSet<UserCalendarNote> UserCalendarNotes { get; set; }

        public DbSet<PropertyFeature> PropertyFeatures { get; set; }

        public DbSet<FeatureCriteriaMap> FeatureCriteriaMaps { get; set; }

        public DbSet<HotelCriteriaFeature> HotelCriteriaFeatures { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {

            //modelBuilder.Entity<Event>()
            //    .HasMany(q => q.municipalities)
            //    .WithMany(x => x.events)
            //    .Map(z =>
            //        z
            //        .MapLeftKey("eventID")
            //        .MapRightKey("kalID")
            //        .ToTable("Disaster_Events_Municipalities_Hurt")
            //        );


        }
    }
}