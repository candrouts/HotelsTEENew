# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**HotelsTEE** is a hotel sustainability certification system ("Σύστημα Πιστοποίησης Βιωσιμότητας") built for TEE (Technical Chamber of Greece). It supports a two-stage workflow: hoteliers self-assess against green criteria, then inspectors review and validate those assessments.

## Build & Run

This is an ASP.NET MVC 5 / Web API 2 project targeting .NET Framework 4.8. Build and run using Visual Studio (open `HotelsTEE.sln`). There is no CLI build script — use Visual Studio's build menu or:

```
msbuild HotelsTEE.sln /p:Configuration=Debug
```

There are no automated tests in this repository.

## Architecture

### Layer structure

```
HotelsTEE/
  Controllers/         # MVC controllers (render views) + Web API controllers (JSON endpoints)
  DAL/                 # Data access: GenericRepository<T>, UnitOfWork, HotelsTEEContext
  Models/              # EF6 entities mapped to TEE_* database tables
  ViewModels/          # DTOs used by raw SQL queries against V_TEE_* database views
  Views/               # Razor views with Knockout.js data binding
  AzureStorage/        # Azure File Share upload/delete helpers
  Utils/               # MD5 encryptor, mailer
  App_Start/           # RouteConfig, WebApiConfig, BundleConfig
```

### Data access pattern

**Reads** use raw SQL against pre-built database views (`V_TEE_*`). Results are deserialized into ViewModels via `context.Database.SqlQuery<TViewModel>(sql)`.

**Writes** use the Unit of Work + Generic Repository pattern: `UnitOfWork` exposes a lazy-initialized `GenericRepository<T>` per entity type, and `unitOfWork.Save()` wraps `context.SaveChanges()`.

All controllers instantiate `UnitOfWork` directly as a field — there is no DI container.

### Frontend

Views use **Knockout.js** for reactive data binding (ko foreach, data-bind attributes throughout). The admin theme is **Hyper** (assets in `~/assets/`). iCheck is used for styled checkboxes/radio buttons.

### Authentication & roles

Forms Authentication backed by SQL Membership Provider. Two user roles matter at runtime:
- `role = 1` — hotelier (accesses Criteria self-assessment)
- `role = 10` — inspector (accesses Certificate review)

Role is read from the `V_TEE_Users` view, not from ASP.NET role tables.

### Two-version workflow

`HotelCriteria.version` distinguishes the two stages:
- `version = 1` — hotel's self-assessment (created on first login)
- `version = 2` — inspector's review copy (cloned from version 1 when inspector opens the certificate)

Submitting the self-assessment (`status = 2`) automatically creates a `HotelierCertificate` record.

### Criteria scoring

Criteria have three types (`criteriaType`):
- `1` — boolean required (checkbox; "No" is locked for required criteria)
- `2` — numeric/select (value × weight = points)
- `3` — boolean optional

Points = `maxGrade × weight`. The medal tier is determined by `totalPoints` against thresholds in `V_TEE_Medals`.

### File storage

Supporting documents are uploaded to **Azure File Share** (`paintings/greencert/{hotelCriteriaID}/{criteriaFileID}/`). The connection string is in `Web.config` under key `StorageConnectionString`. File metadata is persisted in `TEE_HotelCriteria_CriteriaFiles`.

## Key Configuration (Web.config)

- Connection string name: `HotelsTEEContext` (Azure SQL)
- `StorageConnectionString` — Azure File Storage
- `globalization culture="el-GR"` — Greek locale; decimal separator is comma, so value strings use `.Replace(".", ",")` before `Convert.ToDecimal()`
- Forms auth login URL: `~/Account/Login`, timeout 2880 min (sliding)
- Max upload: 1 GB (`maxAllowedContentLength`)

## API Routes

Web API uses attribute routing (`config.MapHttpAttributeRoutes()`). All endpoints are POST:

| Route | Controller | Auth |
|---|---|---|
| `api/AccountApi` (POST) | AccountApiController | Anonymous |
| `api/CriteriaApi/GetAllCriteria` | CriteriaApiController | Authorize |
| `api/CriteriaApi/SaveCriteria` | CriteriaApiController | Authorize |
| `api/CriteriaApi/GetCriteriaFIles` | CriteriaApiController | Authorize |
| `api/CriteriaApi/DeleteFile` | CriteriaApiController | AllowAnonymous |
| `api/CertificateApi/GetAllCertificates` | CertificateApiController | Authorize (role=10) |
| `api/CertificateApi/GetCertificate/{id}` | CertificateApiController | Authorize (role=10) |
| `api/CertificateApi/SaveCriteria` | CertificateApiController | Authorize |
| `api/CertificateApi/UpdateAutopsyDate` | CertificateApiController | Authorize |
| `api/CertificateApi/DeleteFile` | CertificateApiController | AllowAnonymous |
