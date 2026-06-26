using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Web;
using static System.Net.Mime.MediaTypeNames;
using System.Web.Services.Description;
using HotelsTEE.Models;

namespace HotelsTEE.DAL
{
    public class UnitOfWork : IDisposable
    {
        public HotelsTEEContext context = new HotelsTEEContext();

        private GenericRepository<CriteriaCategory> criteriaCategoryRepository;
        public GenericRepository<CriteriaCategory> CriteriaCategoryRepository
        {
            get
            {

                if (this.criteriaCategoryRepository == null)
                {
                    this.criteriaCategoryRepository = new GenericRepository<CriteriaCategory>(context);
                }
                return criteriaCategoryRepository;
            }
        }

        private GenericRepository<Criteria> criteriaRepository;
        public GenericRepository<Criteria> CriteriaRepository
        {
            get
            {

                if (this.criteriaRepository == null)
                {
                    this.criteriaRepository = new GenericRepository<Criteria>(context);
                }
                return criteriaRepository;
            }
        }

        private GenericRepository<Criteria_File> criteria_FileRepository;
        public GenericRepository<Criteria_File> Criteria_FileRepository
        {
            get
            {

                if (this.criteria_FileRepository == null)
                {
                    this.criteria_FileRepository = new GenericRepository<Criteria_File>(context);
                }
                return criteria_FileRepository;
            }
        }

        private GenericRepository<HotelCriteria> hotelCriteriaRepository;
        public GenericRepository<HotelCriteria> HotelCriteriaRepository
        {
            get
            {

                if (this.hotelCriteriaRepository == null)
                {
                    this.hotelCriteriaRepository = new GenericRepository<HotelCriteria>(context);
                }
                return hotelCriteriaRepository;
            }
        }

        private GenericRepository<HotelCriteria_Criteria> hotelCriteria_CriteriaRepository;
        public GenericRepository<HotelCriteria_Criteria> HotelCriteria_CriteriaRepository
        {
            get
            {

                if (this.hotelCriteria_CriteriaRepository == null)
                {
                    this.hotelCriteria_CriteriaRepository = new GenericRepository<HotelCriteria_Criteria>(context);
                }
                return hotelCriteria_CriteriaRepository;
            }
        }

        private GenericRepository<Medal> medalRepository;
        public GenericRepository<Medal> MedalRepository
        {
            get
            {

                if (this.medalRepository == null)
                {
                    this.medalRepository = new GenericRepository<Medal>(context);
                }
                return medalRepository;
            }
        }


        private GenericRepository<Inspector> inspectorRepository;
        public GenericRepository<Inspector> InspectorRepository
        {
            get
            {

                if (this.inspectorRepository == null)
                {
                    this.inspectorRepository = new GenericRepository<Inspector>(context);
                }
                return inspectorRepository;
            }
        }

        private GenericRepository<HotelierCertificate> hotelierCertificateRepository;
        public GenericRepository<HotelierCertificate> HotelierCertificateRepository
        {
            get
            {

                if (this.hotelierCertificateRepository == null)
                {
                    this.hotelierCertificateRepository = new GenericRepository<HotelierCertificate>(context);
                }
                return hotelierCertificateRepository;
            }
        }

        private GenericRepository<HotelCriteria_CriteriaFile> hotelCriteria_CriteriaFileRepository;
        public GenericRepository<HotelCriteria_CriteriaFile> HotelCriteria_CriteriaFileRepository
        {
            get
            {

                if (this.hotelCriteria_CriteriaFileRepository == null)
                {
                    this.hotelCriteria_CriteriaFileRepository = new GenericRepository<HotelCriteria_CriteriaFile>(context);
                }
                return hotelCriteria_CriteriaFileRepository;
            }
        }

        private GenericRepository<InspectorArea> inspectorAreaRepository;
        public GenericRepository<InspectorArea> InspectorAreaRepository
        {
            get
            {
                if (this.inspectorAreaRepository == null)
                    this.inspectorAreaRepository = new GenericRepository<InspectorArea>(context);
                return inspectorAreaRepository;
            }
        }

        private GenericRepository<NotificationTemplate> notificationTemplateRepository;
        public GenericRepository<NotificationTemplate> NotificationTemplateRepository
        {
            get
            {
                if (this.notificationTemplateRepository == null)
                    this.notificationTemplateRepository = new GenericRepository<NotificationTemplate>(context);
                return notificationTemplateRepository;
            }
        }

        private GenericRepository<NotificationLog> notificationLogRepository;
        public GenericRepository<NotificationLog> NotificationLogRepository
        {
            get
            {
                if (this.notificationLogRepository == null)
                    this.notificationLogRepository = new GenericRepository<NotificationLog>(context);
                return notificationLogRepository;
            }
        }

        private GenericRepository<MedalPillarThreshold> medalPillarThresholdRepository;
        public GenericRepository<MedalPillarThreshold> MedalPillarThresholdRepository
        {
            get
            {
                if (this.medalPillarThresholdRepository == null)
                    this.medalPillarThresholdRepository = new GenericRepository<MedalPillarThreshold>(context);
                return medalPillarThresholdRepository;
            }
        }

        private GenericRepository<AppSetting> settingRepository;
        public GenericRepository<AppSetting> SettingRepository
        {
            get
            {
                if (this.settingRepository == null)
                    this.settingRepository = new GenericRepository<AppSetting>(context);
                return settingRepository;
            }
        }

        private GenericRepository<CertificateFile> certificateFileRepository;
        public GenericRepository<CertificateFile> CertificateFileRepository
        {
            get
            {
                if (this.certificateFileRepository == null)
                    this.certificateFileRepository = new GenericRepository<CertificateFile>(context);
                return certificateFileRepository;
            }
        }

        private GenericRepository<CertificateTemplate> certificateTemplateRepository;
        public GenericRepository<CertificateTemplate> CertificateTemplateRepository
        {
            get
            {
                if (this.certificateTemplateRepository == null)
                    this.certificateTemplateRepository = new GenericRepository<CertificateTemplate>(context);
                return certificateTemplateRepository;
            }
        }

        private GenericRepository<UserCalendarNote> userCalendarNoteRepository;
        public GenericRepository<UserCalendarNote> UserCalendarNoteRepository
        {
            get
            {
                if (this.userCalendarNoteRepository == null)
                    this.userCalendarNoteRepository = new GenericRepository<UserCalendarNote>(context);
                return userCalendarNoteRepository;
            }
        }

        private GenericRepository<PropertyFeature> propertyFeatureRepository;
        public GenericRepository<PropertyFeature> PropertyFeatureRepository
        {
            get
            {
                if (this.propertyFeatureRepository == null)
                    this.propertyFeatureRepository = new GenericRepository<PropertyFeature>(context);
                return propertyFeatureRepository;
            }
        }

        private GenericRepository<FeatureCriteriaMap> featureCriteriaMapRepository;
        public GenericRepository<FeatureCriteriaMap> FeatureCriteriaMapRepository
        {
            get
            {
                if (this.featureCriteriaMapRepository == null)
                    this.featureCriteriaMapRepository = new GenericRepository<FeatureCriteriaMap>(context);
                return featureCriteriaMapRepository;
            }
        }

        private GenericRepository<HotelCriteriaFeature> hotelCriteriaFeatureRepository;
        public GenericRepository<HotelCriteriaFeature> HotelCriteriaFeatureRepository
        {
            get
            {
                if (this.hotelCriteriaFeatureRepository == null)
                    this.hotelCriteriaFeatureRepository = new GenericRepository<HotelCriteriaFeature>(context);
                return hotelCriteriaFeatureRepository;
            }
        }

        public void Save()
        {
            context.SaveChanges();
        }

        private bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    context.Dispose();
                }
            }
            this.disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}