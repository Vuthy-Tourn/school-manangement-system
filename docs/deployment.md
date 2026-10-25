# Nexora — Deployment & Operations Guide

## 1. Prerequisites
- **.NET 10.0 SDK** (v10.0.402 or later)
- **Docker 28.0+** & **Docker Compose v2.20+**
- **PostgreSQL 16** (if running bare-metal outside Docker)
- **Xcode 16+ / 27+** (for macOS MacCatalyst & iOS builds on macOS)
- **Android SDK Platform 34/35** (for Android APK/AAB distribution)

---

## 2. Local Environment Setup via Docker Compose

The easiest way to spin up the entire Nexora backend environment with PostgreSQL:

```bash
# 1. Clone repository and navigate to root
cd assignment-student-management

# 2. Configure environment variables
cp .env.example .env

# 3. Start PostgreSQL and API services
docker compose up -d --build

# 4. Verify running services and health status
docker compose ps
```

The database container automatically performs a health check using `pg_isready`. Once healthy, the API starts, applies database migrations, seeds initial data, and listens on port `5180`.

- **Swagger UI:** `http://localhost:5180/swagger`
- **Health Check:** `http://localhost:5180/health`

---

## 3. Database Migration Management

Entity Framework Core migrations are located in `src/EduManage.Infrastructure/Data/Migrations`.

```bash
# Apply migrations to configured database
dotnet ef database update \
  --project src/EduManage.Infrastructure \
  --startup-project src/EduManage.Api

# Add a new migration (after modifying domain entities)
dotnet ef migrations add <MigrationName> \
  --project src/EduManage.Infrastructure \
  --startup-project src/EduManage.Api
```

---

## 4. Building & Running the MAUI Mobile App

### 4.1. Run on macOS (MacCatalyst)
```bash
dotnet build src/EduManage.Mobile/EduManage.Mobile.csproj -f net10.0-maccatalyst
# Run app
dotnet run --project src/EduManage.Mobile/EduManage.Mobile.csproj -f net10.0-maccatalyst
```

### 4.2. Build for Android
```bash
# Debug APK
dotnet build src/EduManage.Mobile/EduManage.Mobile.csproj -f net10.0-android

# Production Release Android App Bundle (.aab)
dotnet publish src/EduManage.Mobile/EduManage.Mobile.csproj \
  -f net10.0-android \
  -c Release \
  /p:AndroidPackageFormat=aab \
  /p:AndroidKeyStore=true \
  /p:AndroidSigningKeyStore=nexora.keystore \
  /p:AndroidSigningStorePass="<your-store-pass>" \
  /p:AndroidSigningKeyAlias=nexora \
  /p:AndroidSigningKeyPass="<your-key-pass>"
```

### 4.3. Build for iOS (on macOS with Xcode)
```bash
# Simulator build
dotnet build src/EduManage.Mobile/EduManage.Mobile.csproj -f net10.0-ios

# Production IPA package
dotnet publish src/EduManage.Mobile/EduManage.Mobile.csproj \
  -f net10.0-ios \
  -c Release \
  /p:ArchiveOnBuild=true \
  /p:CodesignKey="Apple Distribution: Your Org (ID)" \
  /p:CodesignProvision="Your Provisioning Profile Name"
```

---

## 5. Database Backup & Disaster Recovery

### 5.1. Creating a Full Backup
```bash
# For Dockerized PostgreSQL
docker exec -t nexora_postgres pg_dump -U nexora_user -d nexora_school_db -F c -b -v -f /var/lib/postgresql/data/backup_$(date +%Y%m%d_%H%M%S).dump

# Copy backup file to host machine or offsite S3 storage
docker cp nexora_postgres:/var/lib/postgresql/data/backup_*.dump ./backups/
```

### 5.2. Restoring from a Backup
```bash
# Terminate existing connections and restore
docker exec -i nexora_postgres pg_restore -U nexora_user -d nexora_school_db -c -v /var/lib/postgresql/data/backup_filename.dump
```

---

## 6. Production Hardening Checklist

1. [ ] **Secrets:** Replace all default passwords and `JWT_SECRET` in `.env` with strong randomly generated 256-bit keys.
2. [ ] **TLS / SSL:** Terminate TLS using Nginx, Cloudflare, or AWS ALB with HTTP to HTTPS redirection.
3. [ ] **PostgreSQL Network Isolation:** Ensure PostgreSQL port 5432 is not exposed publicly (`ports:` mapped only to internal Docker network).
4. [ ] **CORS Policy:** Restrict allowed origins in `Program.cs` to explicit client domains.
5. [ ] **Rate Limiting:** Enable ASP.NET Core rate limiting on `/api/v1/auth/login` to prevent credential stuffing.
6. [ ] **Backups:** Schedule automated daily `cron` database snapshots with offsite replication.
