# Nexora — Security & Reliability Architecture

## 1. Overview
Nexora is designed in accordance with **OWASP Top 10** principles, **NIST** cryptographic guidelines, and the principle of least privilege. Student academic records, personal identifiable information (PII), and institutional financial transactions are protected at rest, in transit, and at every API boundary.

---

## 2. Authentication & Credential Security

### 2.1. Password Hashing
- **Algorithm:** BCrypt with work factor 11 (`BCrypt.Net-Next`).
- **Salting:** Cryptographically secure per-user salt generated automatically by BCrypt.
- **Protection:** Plaintext passwords are never stored in databases, written to logs, or cached in memory.

### 2.2. JWT Token Architecture
- **Signing:** HMAC using SHA-256 (`HmacSha256Signature`) with a 256-bit or greater symmetric secret.
- **Payload Claims:**
  - `sub`: User unique identifier (`Guid`).
  - `email`: User registered email address.
  - `name`: User's full name.
  - `role`: Role claim (`SuperAdmin`, `SchoolAdmin`, `Teacher`, `Student`, `Parent`).
  - `school_id`: Institutional tenant identifier (`Guid`).
- **Lifespan:** Short-lived access tokens (120 minutes) minimize the window of opportunity if intercepted.

### 2.3. Refresh Token Rotation & Revocation
- **Storage:** Refresh tokens are stored in the database with strict revocation flags (`IsRevoked`), expiry dates (`ExpiresUtc`), and replacement pointers (`ReplacedByToken`).
- **Single-Use Rotation:** When a refresh token is used to issue a new access token, it is immediately revoked and replaced by a newly minted refresh token.
- **Immediate Revocation:** Calling `POST /api/v1/auth/logout` explicitly flags the active token as revoked.

---

## 3. Multi-Tenant Data Isolation

Nexora prevents cross-tenant data leaks at the database query level:

1. **Global Query Filter:**
   ```csharp
   builder.Entity<Student>()
       .HasQueryFilter(e => _currentUserService.Role == UserRole.SuperAdmin || 
                            e.SchoolId == _currentUserService.SchoolId);
   ```
2. **Immutability of Tenant Id:** The client application cannot set or alter `SchoolId` during mutation requests. The server extracts `SchoolId` directly from the authenticated JWT claims.
3. **Cross-Tenant IDOR Prevention:** Any direct object reference (e.g., requesting `/api/v1/students/{id}`) for an entity belonging to a different tenant results in an HTTP 404 (Not Found), confirming zero information disclosure.

---

## 4. Role-Based Authorization Matrix

| Resource / Module | SuperAdmin | SchoolAdmin | Teacher | Student | Parent |
| :--- | :---: | :---: | :---: | :---: | :---: |
| Tenant Provisioning | **CRUD** | - | - | - | - |
| Academic Years & Classes | **CRUD** | **CRUD** | Read | Read | Read |
| Student Records | **CRUD** | **CRUD** | Read (Assigned) | Read (Self) | Read (Children) |
| Teacher Records | **CRUD** | **CRUD** | Read | - | - |
| Daily Attendance | **CRUD** | **CRUD** | **Write (Assigned)** | Read (Self) | Read (Children) |
| Grade Entry | **CRUD** | **CRUD** | **Write (Assigned)** | Read (Self) | Read (Children) |
| Invoices & Payments | **CRUD** | **CRUD** | - | Read (Self) | Read (Children) |
| Timetables | **CRUD** | **CRUD** | Read (Self/Classes) | Read (Self) | Read (Children) |
| System Audit Logs | **Read** | **Read** | - | - | - |

---

## 5. Input Validation & Parameterized Queries

- **FluentValidation:** All client-provided DTOs pass through strict validators checking constraints (max character lengths, required fields, date of birth logic, email formats, score ranges).
- **SQL Injection Prevention:** All database operations utilize Entity Framework Core LINQ queries, which parameterize every query argument automatically via Npgsql.
- **Monetary Integrity:** Financial amounts are validated to ensure non-negative values and calculations use exact `decimal(18,2)` arithmetic.

---

## 6. Mobile Security (.NET MAUI)

1. **Token Persistence:** JWT access and refresh tokens are persisted using **`Microsoft.Maui.Storage.SecureStorage`** (Keychain on macOS/iOS and Android Keystore with EncryptedSharedPreferences on Android).
2. **Network Security:** TLS 1.3 is enforced for all production API endpoints. Android network security config forbids cleartext HTTP traffic in production builds.
3. **Memory Safety:** Sensitive fields (passwords, pin numbers) are cleared from view models immediately after use.

---

## 7. Audit Logging & Non-Repudiation

- High-impact events are captured in the immutable `AuditLogs` table:
  - Account creation, role elevation, and deactivations.
  - Grade entries and subsequent modifications (with before/after scores).
  - Attendance corrections with teacher audit trail.
  - Payment settlements with receipt numbers.
- Sensitive authentication secrets (passwords, tokens) are sanitized and never written to audit tables or Serilog streams.
