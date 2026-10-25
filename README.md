# Nexora — Enterprise School Management System

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-Cross--Platform-512BD4.svg)](https://dotnet.microsoft.com/apps/maui)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791.svg)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-21%20Passed-brightgreen.svg)]()

> **Nexora** is a production-oriented School Management System engineered with **.NET MAUI**, **ASP.NET Core 10**, and **PostgreSQL 16**. Designed for schools, private academies, and educational centers in Cambodia and internationally, Nexora centralizes student admissions, faculty workloads, timetables, daily attendance, authoritative exam grading, and fee invoicing.

---

## 🌟 Key Features

- 🏫 **Multi-Tenant Architecture:** Complete school data isolation powered by EF Core global query filters.
- 🔐 **Zero-Trust Identity & Security:** BCrypt password hashing, JWT access tokens, refresh token rotation, and strict role-based access control (RBAC).
- 👨‍🎓 **Comprehensive Student Management:** Unique admission number generation, demographic profiles, emergency contacts, guardians, and enrollment tracking.
- 👩‍🏫 **Staff & Faculty Scheduling:** Teacher directory, subject specializations, and class assignments.
- 📅 **Conflict-Free Timetables:** Automated collision detection preventing overlapping teacher, room, or class schedules.
- 📋 **Daily Class Attendance:** Fast batch marking (`Present`, `Absent`, `Late`, `Excused`), audit-logged corrections, and automatic attendance percentage calculations.
- 📊 **Authoritative Academic Grading:** Configurable assessment weighting (e.g. Midterm 40%, Final 60%), letter grade thresholds, and credit-weighted GPA calculations.
- 💳 **Billing & Fee Settlements:** Invoicing, partial payments, receipt issuance (supporting ABA Pay, Wing, and bank transfers), and balance tracking.
- 📱 **Cross-Platform .NET MAUI Client:** Modern MVVM client targeting Android, iOS, and macOS (MacCatalyst) with bilingual support (English and Khmer).

---

## 🏗️ Architecture & Solution Structure

Nexora follows **Clean Architecture** and **Domain-Driven Design (DDD)** principles:

```text
assignment-student-management/
├── src/
│   ├── Nexora.Domain/          # Enterprise entities, enums, and business rules
│   ├── Nexora.Application/     # Use cases, DTOs, interfaces, and FluentValidation
│   ├── Nexora.Infrastructure/  # EF Core 10, PostgreSQL, JWT auth, and Seeder
│   ├── Nexora.Api/             # ASP.NET Core 10 REST API and Swagger
│   └── Nexora.Mobile/          # .NET MAUI cross-platform application (MVVM)
├── tests/
│   ├── Nexora.Tests.Unit/        # 14 Unit tests (GPA, collision, billing, security)
│   └── Nexora.Tests.Integration/ # 7 API & DB integration tests (WebApplicationFactory)
├── docs/                          # Detailed architecture, database, API, and security docs
├── docker-compose.yml             # PostgreSQL 16 & API container definitions
└── .env.example                   # Environment configuration template
```

For complete technical specifications, see:
- [System Architecture](file:///Users/macbookpro/Desktop/University/Year%204/Y4S1/MAUI/assignment-student-management/docs/architecture.md)
- [Database Schema & ERD](file:///Users/macbookpro/Desktop/University/Year%204/Y4S1/MAUI/assignment-student-management/docs/database.md)
- [REST API Specification](file:///Users/macbookpro/Desktop/University/Year%204/Y4S1/MAUI/assignment-student-management/docs/api.md)
- [Security & Reliability](file:///Users/macbookpro/Desktop/University/Year%204/Y4S1/MAUI/assignment-student-management/docs/security.md)
- [Deployment Guide](file:///Users/macbookpro/Desktop/University/Year%204/Y4S1/MAUI/assignment-student-management/docs/deployment.md)

---

## 🚀 Quick Start (Development)

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (v10.0.402 or later)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Docker 28.0+ / Compose v2.20+)
- Xcode 16+ / 27+ (on macOS for MacCatalyst and iOS compilation)

### 1. Launch Backend with Docker Compose
```bash
# Clone the repository
cd assignment-student-management

# Configure environment
cp .env.example .env

# Spin up PostgreSQL and API
docker compose up -d --build
```
The API automatically applies database migrations and seeds initial test data upon startup.
- **Swagger Documentation:** [http://localhost:5180/swagger](http://localhost:5180/swagger)
- **Health Check:** [http://localhost:5180/health](http://localhost:5180/health)

### 2. Run Database Migrations Manually (Optional)
If running against a standalone PostgreSQL instance:
```bash
dotnet ef database update \
  --project src/Nexora.Infrastructure \
  --startup-project src/Nexora.Api
```

### 3. Launch the .NET MAUI Mobile App
```bash
# Run on macOS (MacCatalyst)
dotnet run --project src/Nexora.Mobile/Nexora.Mobile.csproj -f net10.0-maccatalyst

# Run on Android Emulator
dotnet build src/Nexora.Mobile/Nexora.Mobile.csproj -f net10.0-android
```

---

## 🔑 Pre-Seeded Demo Credentials

The mobile login screen features a **1-tap quick role selector** at the bottom, or you can log in manually using the following accounts:

| Role | Username / Email | Password | Scope & Responsibilities |
| :--- | :--- | :--- | :--- |
| **Super Administrator** | `superadmin@nexora.edu.kh` | `SuperAdmin123!` | Multi-tenant school management and system oversight |
| **School Administrator** | `admin@angkor.edu.kh` | `Admin123!` | Full school operations, admissions, billing, and scheduling |
| **Teacher** | `teacher.somnang@angkor.edu.kh` | `Teacher123!` | Class attendance, grading, and teaching timetables |
| **Student** | `student.dara@angkor.edu.kh` | `Student123!` | Student dashboard, attendance history, report cards, fees |
| **Parent / Guardian** | `parent.sophal@angkor.edu.kh` | `Parent123!` | Child monitoring, attendance review, and invoice payments |

---

## 🧪 Automated Testing

All business rules, GPA calculations, collision detectors, and API security boundaries are backed by automated tests:

```bash
# Run all solution tests
dotnet test Nexora.sln

# Run unit tests only
dotnet test tests/Nexora.Tests.Unit/Nexora.Tests.Unit.csproj

# Run API integration tests only
dotnet test tests/Nexora.Tests.Integration/Nexora.Tests.Integration.csproj
```

**Test Status:** 21 / 21 Tests Passed (100% Success Rate).

---

## 🛠️ Troubleshooting

- **Database Connection Failure:** Verify PostgreSQL is running (`docker compose ps`) and port 5432 is accessible. Check connection string in `.env`.
- **API Certificate Warning:** Trust local development certificates: `dotnet dev-certs https --trust`.
- **Android Simulator Network:** To access the host machine API from the Android emulator, use `http://10.0.2.2:5180`.

---

## 📄 License
This project is licensed under the MIT License.
