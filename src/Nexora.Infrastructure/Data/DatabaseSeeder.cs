using Nexora.Application.Common;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nexora.Infrastructure.Data;

public class DatabaseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Disable tenant filters during initial seeding
            if (await _context.Schools.IgnoreQueryFilters().AnyAsync())
            {
                _logger.LogInformation("Database already has data. Skipping seeding.");
                return;
            }

            _logger.LogInformation("Starting database seeding for Nexora School Management System...");

            // 1. Create Default School Tenant
            var school = new School
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Angkor International Academy",
                Code = "AIA-KHM",
                Address = "Russian Confederation Blvd, Toul Kork, Phnom Penh, Cambodia",
                PhoneNumber = "+855 23 888 999",
                Email = "info@angkoracademy.edu.kh",
                TimeZoneId = "Asia/Phnom_Penh",
                CurrencyCode = "USD",
                CurrencySymbol = "$",
                IsActive = true,
                SubscriptionPlan = "Enterprise"
            };
            _context.Schools.Add(school);

            // 2. Users & Credentials
            var defaultPasswordHash = _passwordHasher.HashPassword("Admin@123!");
            var teacherPasswordHash = _passwordHasher.HashPassword("Teacher@123!");
            var studentPasswordHash = _passwordHasher.HashPassword("Student@123!");
            var parentPasswordHash = _passwordHasher.HashPassword("Parent@123!");

            // Super Admin
            var superAdmin = new User
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                SchoolId = school.Id,
                Username = "superadmin",
                Email = "superadmin@nexora.cloud",
                PasswordHash = defaultPasswordHash,
                FirstName = "System",
                LastName = "SuperAdmin",
                Role = UserRole.SuperAdmin,
                IsActive = true
            };

            // School Admin
            var schoolAdmin = new User
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                SchoolId = school.Id,
                Username = "admin",
                Email = "admin@angkoracademy.edu.kh",
                PasswordHash = defaultPasswordHash,
                FirstName = "Rithy",
                LastName = "Sam",
                Role = UserRole.SchoolAdmin,
                IsActive = true
            };

            // Teacher Users
            var teacherDaraUser = new User
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                SchoolId = school.Id,
                Username = "teacher.dara",
                Email = "dara.sok@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Dara",
                LastName = "Sok",
                PhoneNumber = "+855 12 345 678",
                Role = UserRole.Teacher,
                IsActive = true
            };

            var teacherChendaUser = new User
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                SchoolId = school.Id,
                Username = "teacher.chenda",
                Email = "chenda.chan@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Chenda",
                LastName = "Chan",
                PhoneNumber = "+855 12 456 789",
                Role = UserRole.Teacher,
                IsActive = true
            };

            // Student Users
            var studentVisalUser = new User
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                SchoolId = school.Id,
                Username = "student.visal",
                Email = "visal.chan@student.angkoracademy.edu.kh",
                PasswordHash = studentPasswordHash,
                FirstName = "Visal",
                LastName = "Chan",
                Role = UserRole.Student,
                IsActive = true
            };

            var studentKanhaUser = new User
            {
                Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                SchoolId = school.Id,
                Username = "student.kanha",
                Email = "kanha.sok@student.angkoracademy.edu.kh",
                PasswordHash = studentPasswordHash,
                FirstName = "Kanha",
                LastName = "Sok",
                Role = UserRole.Student,
                IsActive = true
            };

            // Parent User
            var parentBophaUser = new User
            {
                Id = Guid.Parse("12121212-1212-1212-1212-121212121212"),
                SchoolId = school.Id,
                Username = "parent.bopha",
                Email = "bopha.keo@gmail.com",
                PasswordHash = parentPasswordHash,
                FirstName = "Bopha",
                LastName = "Keo",
                PhoneNumber = "+855 92 112 233",
                Role = UserRole.Parent,
                IsActive = true
            };

            _context.Users.AddRange(superAdmin, schoolAdmin, teacherDaraUser, teacherChendaUser, studentVisalUser, studentKanhaUser, parentBophaUser);

            // 3. Teachers Profile
            var teacherDara = new Teacher
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                UserId = teacherDaraUser.Id,
                EmployeeNumber = "EMP-00101",
                FirstName = "Dara",
                LastName = "Sok",
                Email = teacherDaraUser.Email,
                PhoneNumber = teacherDaraUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2021, 9, 1), DateTimeKind.Utc),
                Qualification = "Master of Mathematics Education (RUPP)",
                Specialization = "Advanced Mathematics & Calculus",
                IsActive = true
            };

            var teacherChenda = new Teacher
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                UserId = teacherChendaUser.Id,
                EmployeeNumber = "EMP-00102",
                FirstName = "Chenda",
                LastName = "Chan",
                Email = teacherChendaUser.Email,
                PhoneNumber = teacherChendaUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2022, 10, 1), DateTimeKind.Utc),
                Qualification = "Bachelor of Science in Physics (ITC)",
                Specialization = "Physics & Applied Science",
                IsActive = true
            };

            _context.Teachers.AddRange(teacherDara, teacherChenda);

            // 4. Academic Structure
            var academicYear = new AcademicYear
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                Name = "2026-2027",
                StartDate = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(new DateTime(2027, 6, 30), DateTimeKind.Utc),
                IsCurrent = true
            };
            _context.AcademicYears.Add(academicYear);

            var semester1 = new Semester
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Name = "Semester 1",
                StartDate = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(new DateTime(2027, 1, 31), DateTimeKind.Utc),
                IsCurrent = true
            };
            var semester2 = new Semester
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                Name = "Semester 2",
                StartDate = DateTime.SpecifyKind(new DateTime(2027, 2, 1), DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(new DateTime(2027, 6, 30), DateTimeKind.Utc),
                IsCurrent = false
            };
            _context.Semesters.AddRange(semester1, semester2);

            var grade10 = new GradeLevel { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Grade 10", LevelOrder = 10 };
            var grade11 = new GradeLevel { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Grade 11", LevelOrder = 11 };
            var grade12 = new GradeLevel { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Grade 12", LevelOrder = 12 };
            _context.GradeLevels.AddRange(grade10, grade11, grade12);

            var class10A = new ClassSection
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                GradeLevelId = grade10.Id,
                Name = "10-A",
                RoomNumber = "Room 201",
                Capacity = 35,
                ClassTeacherId = teacherDara.Id
            };
            var class10B = new ClassSection
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                GradeLevelId = grade10.Id,
                Name = "10-B",
                RoomNumber = "Room 202",
                Capacity = 35,
                ClassTeacherId = teacherChenda.Id
            };
            _context.ClassSections.AddRange(class10A, class10B);

            // 5. Subjects
            var math = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Mathematics", Code = "MATH101", Description = "Core Algebra and Geometry", CreditHours = 4 };
            var khmer = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Khmer Literature", Code = "KHM101", Description = "Cambodian Classical Literature and Grammar", CreditHours = 4 };
            var english = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "English Communication", Code = "ENG101", Description = "Academic English & Composition", CreditHours = 3 };
            var physics = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Physics", Code = "PHY101", Description = "Mechanics and Waves", CreditHours = 3 };
            var chemistry = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Chemistry", Code = "CHM101", Description = "General Chemistry and Laboratory", CreditHours = 3 };
            _context.Subjects.AddRange(math, khmer, english, physics, chemistry);

            // Subject Assignments to Class 10A
            _context.SubjectClassAssignments.AddRange(
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id }
            );

            // 6. Students Profiles
            var studentVisal = new Student
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                UserId = studentVisalUser.Id,
                AdmissionNumber = "ADM-2026-0001",
                FirstName = "Visal",
                LastName = "Chan",
                DateOfBirth = DateTime.SpecifyKind(new DateTime(2010, 5, 14), DateTimeKind.Utc),
                Gender = "Male",
                BloodGroup = "O+",
                EnrollmentDate = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc),
                Status = StudentStatus.Active,
                Email = studentVisalUser.Email,
                PhoneNumber = "+855 98 123 456",
                Address = "Preah Sihanouk Blvd, Phnom Penh",
                CurrentClassSectionId = class10A.Id,
                EmergencyContactName = "Keo Bopha",
                EmergencyContactPhone = "+855 92 112 233",
                EmergencyContactRelationship = "Mother"
            };

            var studentKanha = new Student
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                UserId = studentKanhaUser.Id,
                AdmissionNumber = "ADM-2026-0002",
                FirstName = "Kanha",
                LastName = "Sok",
                DateOfBirth = DateTime.SpecifyKind(new DateTime(2010, 8, 22), DateTimeKind.Utc),
                Gender = "Female",
                BloodGroup = "A+",
                EnrollmentDate = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc),
                Status = StudentStatus.Active,
                Email = studentKanhaUser.Email,
                PhoneNumber = "+855 98 654 321",
                Address = "Norodom Blvd, Phnom Penh",
                CurrentClassSectionId = class10A.Id,
                EmergencyContactName = "Sok Vichea",
                EmergencyContactPhone = "+855 97 554 332",
                EmergencyContactRelationship = "Father"
            };
            _context.Students.AddRange(studentVisal, studentKanha);

            // Student Enrollments
            _context.StudentEnrollments.AddRange(
                new StudentEnrollment { Id = Guid.NewGuid(), SchoolId = school.Id, StudentId = studentVisal.Id, ClassSectionId = class10A.Id, AcademicYearId = academicYear.Id, RollNumber = 1, EnrollmentDate = DateTime.UtcNow, Status = EnrollmentStatus.Enrolled },
                new StudentEnrollment { Id = Guid.NewGuid(), SchoolId = school.Id, StudentId = studentKanha.Id, ClassSectionId = class10A.Id, AcademicYearId = academicYear.Id, RollNumber = 2, EnrollmentDate = DateTime.UtcNow, Status = EnrollmentStatus.Enrolled }
            );

            // 7. Parent Profile and Link
            var parentBopha = new ParentGuardian
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                UserId = parentBophaUser.Id,
                FirstName = "Bopha",
                LastName = "Keo",
                Email = parentBophaUser.Email,
                PhoneNumber = parentBophaUser.PhoneNumber!,
                Occupation = "Senior Accountant",
                Address = "Preah Sihanouk Blvd, Phnom Penh"
            };
            _context.ParentGuardians.Add(parentBopha);

            _context.StudentGuardians.Add(new StudentGuardian
            {
                Id = Guid.NewGuid(),
                StudentId = studentVisal.Id,
                GuardianId = parentBopha.Id,
                Relationship = "Mother",
                IsPrimaryContact = true
            });

            // 8. Timetable Entries
            _context.TimetableEntries.AddRange(
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Physics Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(13, 30, 0), EndTime = new TimeSpan(15, 0, 0), RoomNumber = "Physics Lab" }
            );

            // 9. Attendance Records for past 5 days
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            for (int i = 0; i < 5; i++)
            {
                var date = today.AddDays(-i);
                _context.AttendanceRecords.AddRange(
                    new AttendanceRecord { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, StudentId = studentVisal.Id, Date = date, Status = AttendanceStatus.Present, RecordedByUserId = teacherDaraUser.Id, RecordedAtUtc = DateTime.UtcNow },
                    new AttendanceRecord { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, StudentId = studentKanha.Id, Date = date, Status = (i == 1 ? AttendanceStatus.Late : AttendanceStatus.Present), Remarks = (i == 1 ? "Traffic delay" : null), RecordedByUserId = teacherDaraUser.Id, RecordedAtUtc = DateTime.UtcNow }
                );
            }

            // 10. Grade Scales (Cambodian / International Standard)
            _context.GradeScales.AddRange(
                new GradeScale { Id = Guid.NewGuid(), SchoolId = school.Id, GradeLetter = "A", MinPercentage = 90.0m, MaxPercentage = 100.0m, GradePoint = 4.0m, Description = "Excellent" },
                new GradeScale { Id = Guid.NewGuid(), SchoolId = school.Id, GradeLetter = "B", MinPercentage = 80.0m, MaxPercentage = 89.9m, GradePoint = 3.0m, Description = "Very Good" },
                new GradeScale { Id = Guid.NewGuid(), SchoolId = school.Id, GradeLetter = "C", MinPercentage = 70.0m, MaxPercentage = 79.9m, GradePoint = 2.0m, Description = "Good" },
                new GradeScale { Id = Guid.NewGuid(), SchoolId = school.Id, GradeLetter = "D", MinPercentage = 60.0m, MaxPercentage = 69.9m, GradePoint = 1.0m, Description = "Satisfactory" },
                new GradeScale { Id = Guid.NewGuid(), SchoolId = school.Id, GradeLetter = "E", MinPercentage = 50.0m, MaxPercentage = 59.9m, GradePoint = 0.5m, Description = "Pass" },
                new GradeScale { Id = Guid.NewGuid(), SchoolId = school.Id, GradeLetter = "F", MinPercentage = 0.0m, MaxPercentage = 49.9m, GradePoint = 0.0m, Description = "Fail" }
            );

            // 11. Exams & Assessments
            var exam = new Exam
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                AcademicYearId = academicYear.Id,
                SemesterId = semester1.Id,
                Name = "Semester 1 Midterm Examination",
                StartDate = DateTime.SpecifyKind(new DateTime(2026, 11, 15), DateTimeKind.Utc),
                EndDate = DateTime.SpecifyKind(new DateTime(2026, 11, 25), DateTimeKind.Utc),
                IsPublished = true,
                PublishedAtUtc = DateTime.UtcNow
            };
            _context.Exams.Add(exam);

            var mathComp = new AssessmentComponent
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                ExamId = exam.Id,
                SubjectId = math.Id,
                ClassSectionId = class10A.Id,
                Name = "Midterm Written Exam",
                MaxScore = 100m,
                WeightPercentage = 100m
            };

            var phyComp = new AssessmentComponent
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                ExamId = exam.Id,
                SubjectId = physics.Id,
                ClassSectionId = class10A.Id,
                Name = "Physics Practical & Theory",
                MaxScore = 100m,
                WeightPercentage = 100m
            };
            _context.AssessmentComponents.AddRange(mathComp, phyComp);

            // Student Grades
            _context.StudentGrades.AddRange(
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = mathComp.Id, StudentId = studentVisal.Id, ScoreObtained = 94.5m, Remarks = "Outstanding analytical ability", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = phyComp.Id, StudentId = studentVisal.Id, ScoreObtained = 88.0m, Remarks = "Good experimental report", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = mathComp.Id, StudentId = studentKanha.Id, ScoreObtained = 89.0m, Remarks = "Well done", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow }
            );

            // 12. Fee Categories & Invoices
            var tuitionCat = new FeeCategory { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Tuition Fee - Semester 1", DefaultAmount = 650.0m, Description = "Academic tuition fee per semester", IsActive = true };
            var labCat = new FeeCategory { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Science Lab & Technology Fee", DefaultAmount = 80.0m, Description = "Annual lab materials and equipment usage", IsActive = true };
            _context.FeeCategories.AddRange(tuitionCat, labCat);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                StudentId = studentVisal.Id,
                AcademicYearId = academicYear.Id,
                InvoiceNumber = "INV-2026-00001",
                IssueDate = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc),
                DueDate = DateTime.SpecifyKind(new DateTime(2026, 10, 15), DateTimeKind.Utc),
                TotalAmount = 730.0m,
                PaidAmount = 500.0m,
                Status = InvoiceStatus.PartiallyPaid,
                Notes = "First semester fee package"
            };

            invoice.Items.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoice.Id, FeeCategoryId = tuitionCat.Id, Description = "Tuition Fee - Semester 1", Amount = 650.0m });
            invoice.Items.Add(new InvoiceItem { Id = Guid.NewGuid(), InvoiceId = invoice.Id, FeeCategoryId = labCat.Id, Description = "Science Lab Fee", Amount = 80.0m });
            _context.Invoices.Add(invoice);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                InvoiceId = invoice.Id,
                StudentId = studentVisal.Id,
                ReceiptNumber = "REC-2026-00001",
                Amount = 500.0m,
                PaymentDate = DateTime.SpecifyKind(new DateTime(2026, 9, 5), DateTimeKind.Utc),
                PaymentMethod = PaymentMethod.ABA,
                ReferenceNumber = "ABA-PAY-981245781",
                Notes = "Partial payment via ABA Pay",
                ReceivedByUserId = schoolAdmin.Id
            };
            _context.Payments.Add(payment);

            // 13. Announcements
            _context.Announcements.AddRange(
                new Announcement
                {
                    Id = Guid.NewGuid(),
                    SchoolId = school.Id,
                    Title = "Welcome to the Academic Year 2026-2027!",
                    Content = "Angkor International Academy welcomes all teachers, students, and parents to the new academic year. Please review the updated class schedules in the mobile app.",
                    TargetRole = AnnouncementTarget.All,
                    Priority = AnnouncementPriority.High,
                    PublishedByUserId = schoolAdmin.Id,
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-10)
                },
                new Announcement
                {
                    Id = Guid.NewGuid(),
                    SchoolId = school.Id,
                    Title = "Midterm Examination Schedule Announcement",
                    Content = "The Semester 1 Midterm Examinations will commence on November 15. All students are advised to prepare accordingly.",
                    TargetRole = AnnouncementTarget.All,
                    Priority = AnnouncementPriority.Urgent,
                    PublishedByUserId = schoolAdmin.Id,
                    PublishedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            );

            // 14. Initial Audit Log
            _context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                SchoolId = school.Id,
                UserId = schoolAdmin.Id,
                Action = AuditAction.Create,
                EntityName = "System",
                Details = "Initial school configuration and seed data initialized.",
                TimestampUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Database seeded successfully with Angkor International Academy!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }
}
