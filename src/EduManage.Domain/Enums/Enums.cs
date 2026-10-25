namespace EduManage.Domain.Enums;

public enum UserRole
{
    SuperAdmin = 1,
    SchoolAdmin = 2,
    Teacher = 3,
    Student = 4,
    Parent = 5
}

public enum StudentStatus
{
    Active = 1,
    Suspended = 2,
    Graduated = 3,
    Transferred = 4,
    Inactive = 5
}

public enum EnrollmentStatus
{
    Enrolled = 1,
    Promoted = 2,
    Repeated = 3,
    Transferred = 4,
    Dropped = 5
}

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3,
    Excused = 4
}

public enum InvoiceStatus
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    ABA = 3,
    Wing = 4,
    Bakong = 5,
    CreditDebitCard = 6,
    Other = 7
}

public enum AnnouncementTarget
{
    All = 1,
    Teachers = 2,
    Students = 3,
    Parents = 4,
    SpecificClass = 5
}

public enum AnnouncementPriority
{
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum NotificationType
{
    General = 1,
    Attendance = 2,
    Grade = 3,
    Fee = 4,
    Timetable = 5
}

public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    Publish = 4,
    Login = 5,
    GradeChange = 6,
    AttendanceCorrection = 7
}
