# Nexora — REST API Specification & Endpoint Catalog

## 1. Overview
The **Nexora.Api** service exposes a secure, high-performance RESTful Web API built on ASP.NET Core 10. All payloads and responses are formatted in UTF-8 JSON.

- **Base URL:** `https://api.nexora.edu.kh` (Production) / `http://localhost:5180` (Development)
- **API Versioning:** `/api/v1/` prefix for all operational routes.
- **Swagger / OpenAPI:** Available in development at `/swagger/index.html`.
- **Health Check:** `GET /health` returns status of the API and PostgreSQL connectivity.

---

## 2. Authentication & Headers

Every protected request must provide a valid JWT Bearer token in the `Authorization` HTTP header:

```http
Authorization: Bearer <jwt_access_token>
Content-Type: application/json
Accept: application/json
```

### Response Envelope Standard
All successful and business-layer responses adhere to the standard `ApiResponse<T>` envelope:
```json
{
  "success": true,
  "data": { ... },
  "message": "Operation completed successfully.",
  "errors": []
}
```

### Error Response Standard (RFC 7807)
Uncaught exceptions and validation failures return RFC 7807 ProblemDetails:
```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation failures occurred.",
  "errors": {
    "Email": ["The specified email format is invalid."],
    "Score": ["Score cannot exceed the maximum assessment marks."]
  }
}
```

---

## 3. Endpoints Catalog

### 3.1. Authentication (`/api/v1/auth`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/auth/login` | Anonymous | Authenticates credentials and returns JWT + Refresh Token. |
| `POST` | `/api/v1/auth/refresh-token` | Anonymous | Issues new access token given a valid refresh token. |
| `POST` | `/api/v1/auth/change-password` | Authenticated | Updates the current user's password. |
| `GET` | `/api/v1/auth/profile` | Authenticated | Retrieves current user profile and role details. |

### 3.2. Students (`/api/v1/students`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/students` | Admin, Teacher | Paginated, searchable student directory. Query params: `page`, `pageSize`, `search`, `classId`, `status`. |
| `GET` | `/api/v1/students/{id}` | Admin, Teacher, Student, Parent | Detailed profile including guardian contacts and enrollment. |
| `POST` | `/api/v1/students` | Admin, SuperAdmin | Registers student profile with auto-generated admission number. |
| `PUT` | `/api/v1/students/{id}` | Admin, SuperAdmin | Updates student demographic and address info. |
| `DELETE` | `/api/v1/students/{id}` | Admin, SuperAdmin | Soft-deactivates student record (sets status to `Inactive`). |

### 3.3. Teachers (`/api/v1/teachers`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/teachers` | Admin, SuperAdmin | Lists all faculty members and subject specializations. |
| `POST` | `/api/v1/teachers` | Admin, SuperAdmin | Provisions new teacher account and faculty record. |

### 3.4. Academic Structure (`/api/v1/academics`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/academics/years` | Authenticated | Lists academic school calendar years. |
| `POST` | `/api/v1/academics/years` | Admin, SuperAdmin | Creates a new academic year. |
| `GET` | `/api/v1/academics/classes` | Authenticated | Lists class sections (e.g. 10-A, 10-B) with homeroom teachers. |
| `POST` | `/api/v1/academics/classes` | Admin, SuperAdmin | Creates a new class section. |
| `GET` | `/api/v1/academics/subjects` | Authenticated | Curricular subjects catalog with credit hours. |
| `POST` | `/api/v1/academics/subjects` | Admin, SuperAdmin | Creates new curricular subject. |
| `POST` | `/api/v1/academics/enrollments` | Admin, SuperAdmin | Enrolls or promotes a student into a class section. |

### 3.5. Timetables & Scheduling (`/api/v1/timetables`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/timetables/class/{classId}` | Authenticated | Weekly timetable entries for a class section. |
| `GET` | `/api/v1/timetables/teacher/{teacherId}` | Authenticated | Teaching timetable schedule for a faculty member. |
| `POST` | `/api/v1/timetables` | Admin, SuperAdmin | Schedules a period with automated teacher/room/class conflict detection. |

### 3.6. Attendance Management (`/api/v1/attendance`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/attendance` | Authenticated | Retrieves daily class attendance list. Query: `classId`, `date`. |
| `POST` | `/api/v1/attendance/record-batch` | Admin, Teacher | Records or updates daily class attendance with audit trail. |
| `GET` | `/api/v1/attendance/summary/student/{studentId}` | Authenticated | Attendance statistics (Present %, Absent, Late, Excused counts). |

### 3.7. Exams & Grades (`/api/v1/grades`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/grades/exams` | Authenticated | Lists academic exams and assessment components. |
| `POST` | `/api/v1/grades/exams` | Admin, Teacher | Creates exam with weighted assessment components. |
| `POST` | `/api/v1/grades/record-batch` | Admin, Teacher | Enters student marks and validates against component maximums. |
| `GET` | `/api/v1/grades/report-card/student/{studentId}` | Authenticated | Generates authoritative report card with percentage, letter grade, and GPA. |

### 3.8. Fees & Invoicing (`/api/v1/fees`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/fees/invoices` | Authenticated | Filtered invoice list. Query: `studentId`, `academicYearId`, `status`. |
| `POST` | `/api/v1/fees/invoices` | Admin, SuperAdmin | Issues student invoice with itemized charges. |
| `POST` | `/api/v1/fees/payments` | Admin, SuperAdmin | Records payment (Cash, ABA Pay, Wing, Bank Transfer) and generates receipt. |

### 3.9. Announcements (`/api/v1/announcements`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/announcements` | Authenticated | Active announcements targeted to user role or school. |
| `POST` | `/api/v1/announcements` | Admin, Teacher | Publishes announcement with audience targeting and priority. |

### 3.10. Dashboards (`/api/v1/dashboards`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/dashboards/admin` | Admin, SuperAdmin | Total counts (students, teachers, classes), attendance %, revenue summary. |
| `GET` | `/api/v1/dashboards/teacher` | Teacher | Assigned classes, today's schedule, quick attendance shortcuts. |
| `GET` | `/api/v1/dashboards/student` | Student | Overall GPA, attendance percentage, class schedule, outstanding balance. |

### 3.11. Reports & Exports (`/api/v1/reports`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/reports/enrollment/csv` | Admin, SuperAdmin | Exports student enrollment directory to CSV format. |
| `GET` | `/api/v1/reports/attendance/csv` | Admin, SuperAdmin | Exports class attendance logs to CSV format. |
| `GET` | `/api/v1/reports/fees/csv` | Admin, SuperAdmin | Exports billing and collections ledger to CSV format. |

### 3.12. Audit Logs (`/api/v1/auditlogs`)
| Method | Endpoint | Authorization | Description |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/auditlogs` | Admin, SuperAdmin | Paginated system audit trail for security compliance. |
