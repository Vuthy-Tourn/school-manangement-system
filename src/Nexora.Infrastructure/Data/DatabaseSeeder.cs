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
                var existingSchool = await _context.Schools.IgnoreQueryFilters().FirstAsync();
                await EnsureRosterAndTimetableAsync(existingSchool);
                _logger.LogInformation("Database already has data. Ensured full roster and timetable.");
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

            var teacherSokhaUser = new User
            {
                Id = Guid.Parse("c0000001-0000-0000-0000-000000000001"),
                SchoolId = school.Id,
                Username = "teacher.sokha",
                Email = "sokha.mean@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Sokha",
                LastName = "Mean",
                PhoneNumber = "+855 12 567 890",
                Role = UserRole.Teacher,
                IsActive = true
            };

            var teacherRathanaUser = new User
            {
                Id = Guid.Parse("c0000001-0000-0000-0000-000000000004"),
                SchoolId = school.Id,
                Username = "teacher.rathana",
                Email = "rathana.vong@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Rathana",
                LastName = "Vong",
                PhoneNumber = "+855 12 678 901",
                Role = UserRole.Teacher,
                IsActive = true
            };

            var teacherJohnUser = new User
            {
                Id = Guid.Parse("c0000001-0000-0000-0000-000000000005"),
                SchoolId = school.Id,
                Username = "teacher.john",
                Email = "john.smith@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "John",
                LastName = "Smith",
                PhoneNumber = "+855 12 789 012",
                Role = UserRole.Teacher,
                IsActive = true
            };

            var teacherKolabUser = new User
            {
                Id = Guid.Parse("c0000001-0000-0000-0000-000000000006"),
                SchoolId = school.Id,
                Username = "teacher.kolab",
                Email = "kolab.pich@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Kolab",
                LastName = "Pich",
                PhoneNumber = "+855 12 890 123",
                Role = UserRole.Teacher,
                IsActive = true
            };

            var teacherSophalUser = new User
            {
                Id = Guid.Parse("c0000001-0000-0000-0000-000000000007"),
                SchoolId = school.Id,
                Username = "teacher.sophal",
                Email = "sophal.tep@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Sophal",
                LastName = "Tep",
                PhoneNumber = "+855 12 901 234",
                Role = UserRole.Teacher,
                IsActive = true
            };

            var teacherSamnangUser = new User
            {
                Id = Guid.Parse("c0000001-0000-0000-0000-000000000008"),
                SchoolId = school.Id,
                Username = "teacher.samnang",
                Email = "samnang.ly@angkoracademy.edu.kh",
                PasswordHash = teacherPasswordHash,
                FirstName = "Samnang",
                LastName = "Ly",
                PhoneNumber = "+855 12 012 345",
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

            _context.Users.AddRange(
                superAdmin, schoolAdmin,
                teacherDaraUser, teacherChendaUser, teacherSokhaUser, teacherRathanaUser,
                teacherJohnUser, teacherKolabUser, teacherSophalUser, teacherSamnangUser,
                studentVisalUser, studentKanhaUser, parentBophaUser);

            // 3. Teachers Profile
            var teacherDara = new Teacher
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
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
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
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

            var teacherSokha = new Teacher
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
                SchoolId = school.Id,
                UserId = teacherSokhaUser.Id,
                EmployeeNumber = "EMP-00103",
                FirstName = "Sokha",
                LastName = "Mean",
                Email = teacherSokhaUser.Email,
                PhoneNumber = teacherSokhaUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2020, 8, 15), DateTimeKind.Utc),
                Qualification = "Master of Khmer Literature (RUPP)",
                Specialization = "Classical Khmer Literature & Poetry",
                IsActive = true
            };

            var teacherRathana = new Teacher
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000004"),
                SchoolId = school.Id,
                UserId = teacherRathanaUser.Id,
                EmployeeNumber = "EMP-00104",
                FirstName = "Rathana",
                LastName = "Vong",
                Email = teacherRathanaUser.Email,
                PhoneNumber = teacherRathanaUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2021, 11, 1), DateTimeKind.Utc),
                Qualification = "Master of Chemical Sciences (ITC)",
                Specialization = "General & Organic Chemistry",
                IsActive = true
            };

            var teacherJohn = new Teacher
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000005"),
                SchoolId = school.Id,
                UserId = teacherJohnUser.Id,
                EmployeeNumber = "EMP-00105",
                FirstName = "John",
                LastName = "Smith",
                Email = teacherJohnUser.Email,
                PhoneNumber = teacherJohnUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2019, 7, 1), DateTimeKind.Utc),
                Qualification = "MA in Applied Linguistics (Melbourne)",
                Specialization = "English Communication & Academic Writing",
                IsActive = true
            };

            var teacherKolab = new Teacher
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000006"),
                SchoolId = school.Id,
                UserId = teacherKolabUser.Id,
                EmployeeNumber = "EMP-00106",
                FirstName = "Kolab",
                LastName = "Pich",
                Email = teacherKolabUser.Email,
                PhoneNumber = teacherKolabUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2022, 1, 10), DateTimeKind.Utc),
                Qualification = "M.Sc in Biological Sciences (RUPP)",
                Specialization = "Cellular Biology & Genetics",
                IsActive = true
            };

            var teacherSophal = new Teacher
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000007"),
                SchoolId = school.Id,
                UserId = teacherSophalUser.Id,
                EmployeeNumber = "EMP-00107",
                FirstName = "Sophal",
                LastName = "Tep",
                Email = teacherSophalUser.Email,
                PhoneNumber = teacherSophalUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2020, 5, 20), DateTimeKind.Utc),
                Qualification = "Master of Southeast Asian History (RUPP)",
                Specialization = "Cambodian History & Civilization",
                IsActive = true
            };

            var teacherSamnang = new Teacher
            {
                Id = Guid.Parse("c0000000-0000-0000-0000-000000000008"),
                SchoolId = school.Id,
                UserId = teacherSamnangUser.Id,
                EmployeeNumber = "EMP-00108",
                FirstName = "Samnang",
                LastName = "Ly",
                Email = teacherSamnangUser.Email,
                PhoneNumber = teacherSamnangUser.PhoneNumber,
                HireDate = DateTime.SpecifyKind(new DateTime(2023, 2, 1), DateTimeKind.Utc),
                Qualification = "B.Sc in Software Engineering (RUPP)",
                Specialization = "Computer Science & ICT",
                IsActive = true
            };

            _context.Teachers.AddRange(teacherDara, teacherChenda, teacherSokha, teacherRathana, teacherJohn, teacherKolab, teacherSophal, teacherSamnang);

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
            var biology = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Biology", Code = "BIO101", Description = "Cellular Biology and Genetics", CreditHours = 3 };
            var history = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "History", Code = "HIS101", Description = "Cambodian and World History", CreditHours = 2 };
            var ict = new Subject { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Computer Science & ICT", Code = "ICT101", Description = "Programming, Algorithms & Systems", CreditHours = 2 };
            _context.Subjects.AddRange(math, khmer, english, physics, chemistry, biology, history, ict);

            // Subject Assignments to Class 10A and 10B
            _context.SubjectClassAssignments.AddRange(
                // 10-A
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = teacherSokha.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = teacherJohn.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = chemistry.Id, TeacherId = teacherRathana.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = biology.Id, TeacherId = teacherKolab.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = history.Id, TeacherId = teacherSophal.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = ict.Id, TeacherId = teacherSamnang.Id },

                // 10-B
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = math.Id, TeacherId = teacherDara.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = khmer.Id, TeacherId = teacherSokha.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = english.Id, TeacherId = teacherJohn.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = chemistry.Id, TeacherId = teacherRathana.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = biology.Id, TeacherId = teacherKolab.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = history.Id, TeacherId = teacherSophal.Id },
                new SubjectClassAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = ict.Id, TeacherId = teacherSamnang.Id }
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

            _context.StudentGuardians.AddRange(
                new StudentGuardian
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentVisal.Id,
                    GuardianId = parentBopha.Id,
                    Relationship = "Mother",
                    IsPrimaryContact = true
                },
                new StudentGuardian
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentKanha.Id,
                    GuardianId = parentBopha.Id,
                    Relationship = "Mother",
                    IsPrimaryContact = false
                }
            );

            // 8. Timetable Entries (Full Monday - Friday Schedule for Class 10A with genuine subject teachers)
            _context.TimetableEntries.AddRange(
                // Monday
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Physics Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = teacherSokha.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = teacherJohn.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Room 201" },

                // Tuesday
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = chemistry.Id, TeacherId = teacherRathana.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Chemistry Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = biology.Id, TeacherId = teacherKolab.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Biology Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = history.Id, TeacherId = teacherSophal.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Room 201" },

                // Wednesday
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = ict.Id, TeacherId = teacherSamnang.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Computer Lab 1" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = teacherSokha.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = teacherJohn.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Physics Lab" },

                // Thursday
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = teacherDara.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = chemistry.Id, TeacherId = teacherRathana.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Chemistry Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = history.Id, TeacherId = teacherSophal.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = biology.Id, TeacherId = teacherKolab.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Biology Lab" },

                // Friday
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = teacherSokha.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = teacherJohn.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Room 201" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = ict.Id, TeacherId = teacherSamnang.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Computer Lab 1" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Physics Lab" },

                // Class 10-B Timetable Entries (Mon - Fri)
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = khmer.Id, TeacherId = teacherSokha.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 202" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = math.Id, TeacherId = teacherDara.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Room 202" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = physics.Id, TeacherId = teacherChenda.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Physics Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = chemistry.Id, TeacherId = teacherRathana.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Chemistry Lab" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = english.Id, TeacherId = teacherJohn.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 202" },
                new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10B.Id, SubjectId = biology.Id, TeacherId = teacherKolab.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Biology Lab" }
            );

            // 9. Attendance Records for past 10 days (Visal Chan: 100% Present)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            for (int i = 0; i < 10; i++)
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

            var mathComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = math.Id, ClassSectionId = class10A.Id, Name = "Midterm Written Exam", MaxScore = 100m, WeightPercentage = 40m };
            var phyComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = physics.Id, ClassSectionId = class10A.Id, Name = "Physics Practical & Theory", MaxScore = 100m, WeightPercentage = 40m };
            var khmerComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = khmer.Id, ClassSectionId = class10A.Id, Name = "Khmer Composition Exam", MaxScore = 100m, WeightPercentage = 40m };
            var engComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = english.Id, ClassSectionId = class10A.Id, Name = "English Listening & Essay", MaxScore = 100m, WeightPercentage = 40m };
            var chemComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = chemistry.Id, ClassSectionId = class10A.Id, Name = "Chemistry Midterm Exam", MaxScore = 100m, WeightPercentage = 40m };
            var bioComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = biology.Id, ClassSectionId = class10A.Id, Name = "Biology Laboratory & Theory", MaxScore = 100m, WeightPercentage = 40m };
            var histComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = history.Id, ClassSectionId = class10A.Id, Name = "History Midterm Essay", MaxScore = 100m, WeightPercentage = 40m };
            var ictComp = new AssessmentComponent { Id = Guid.NewGuid(), SchoolId = school.Id, ExamId = exam.Id, SubjectId = ict.Id, ClassSectionId = class10A.Id, Name = "ICT Practical Project", MaxScore = 100m, WeightPercentage = 40m };

            _context.AssessmentComponents.AddRange(mathComp, phyComp, khmerComp, engComp, chemComp, bioComp, histComp, ictComp);

            // Student Grades
            _context.StudentGrades.AddRange(
                // Visal Chan
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = mathComp.Id, StudentId = studentVisal.Id, ScoreObtained = 95.0m, Remarks = "Outstanding analytical ability", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = khmerComp.Id, StudentId = studentVisal.Id, ScoreObtained = 92.0m, Remarks = "Mastery of Khmer grammar and rhetoric", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = engComp.Id, StudentId = studentVisal.Id, ScoreObtained = 94.0m, Remarks = "Fluent expression and essay writing", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = phyComp.Id, StudentId = studentVisal.Id, ScoreObtained = 88.0m, Remarks = "Good experimental report", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = chemComp.Id, StudentId = studentVisal.Id, ScoreObtained = 86.5m, Remarks = "Solid chemical reactions understanding", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = bioComp.Id, StudentId = studentVisal.Id, ScoreObtained = 90.0m, Remarks = "Great grasp of genetics and ecology", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = histComp.Id, StudentId = studentVisal.Id, ScoreObtained = 91.5m, Remarks = "Thorough knowledge of history", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = ictComp.Id, StudentId = studentVisal.Id, ScoreObtained = 98.0m, Remarks = "Exceptional software development skills", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },

                // Kanha Sok
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = mathComp.Id, StudentId = studentKanha.Id, ScoreObtained = 89.0m, Remarks = "Well done", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = khmerComp.Id, StudentId = studentKanha.Id, ScoreObtained = 95.0m, Remarks = "Top class score", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = engComp.Id, StudentId = studentKanha.Id, ScoreObtained = 97.0m, Remarks = "Excellent communication skills", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = phyComp.Id, StudentId = studentKanha.Id, ScoreObtained = 84.0m, Remarks = "Good lab work and scientific writeup", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = chemComp.Id, StudentId = studentKanha.Id, ScoreObtained = 91.0m, Remarks = "Excellent laboratory procedure compliance", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = bioComp.Id, StudentId = studentKanha.Id, ScoreObtained = 88.0m, Remarks = "Solid grasp of evolutionary biology", GradedByUserId = teacherChendaUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = histComp.Id, StudentId = studentKanha.Id, ScoreObtained = 93.0m, Remarks = "Active historical analysis and source evaluation", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow },
                new StudentGrade { Id = Guid.NewGuid(), SchoolId = school.Id, AssessmentComponentId = ictComp.Id, StudentId = studentKanha.Id, ScoreObtained = 94.0m, Remarks = "Well executed interactive website project", GradedByUserId = teacherDaraUser.Id, GradedAtUtc = DateTime.UtcNow }
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

    private async Task EnsureRosterAndTimetableAsync(School school)
    {
        var class10A = await _context.ClassSections.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.SchoolId == school.Id && c.Name == "10-A");
        if (class10A == null) return;

        var academicYear = await _context.AcademicYears.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.SchoolId == school.Id && a.IsCurrent)
            ?? await _context.AcademicYears.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.SchoolId == school.Id);
        if (academicYear == null) return;

        var existingStudents = await _context.Students.IgnoreQueryFilters().Where(s => s.SchoolId == school.Id).ToListAsync();
        var studentPasswordHash = _passwordHasher.HashPassword("Student@123!");

        var additionalStudents = new List<(string Adm, string First, string Last, string Gender, string Email, string Phone, string Address, string EmergencyName, string EmergencyPhone, string Rel)>
        {
            ("ADM-2026-0003", "Dara", "Meng", "Male", "dara.meng@student.angkoracademy.edu.kh", "+855 98 333 444", "Toul Kork, Phnom Penh", "Meng Chhay", "+855 12 999 888", "Father"),
            ("ADM-2026-0004", "Bopha", "Keo", "Female", "bopha.keo@student.angkoracademy.edu.kh", "+855 98 777 888", "Chamkarmon, Phnom Penh", "Keo Sarath", "+855 12 777 666", "Father"),
            ("ADM-2026-0005", "Rithy", "Srun", "Male", "rithy.srun@student.angkoracademy.edu.kh", "+855 98 111 222", "Khan Sen Sok, Phnom Penh", "Srun Meng", "+855 12 111 333", "Father"),
            ("ADM-2026-0006", "Sreypov", "Lim", "Female", "sreypov.lim@student.angkoracademy.edu.kh", "+855 98 222 333", "Khan Daun Penh, Phnom Penh", "Lim Sovann", "+855 12 222 444", "Mother"),
            ("ADM-2026-0007", "Vannak", "Heng", "Male", "vannak.heng@student.angkoracademy.edu.kh", "+855 98 333 444", "Khan Toul Kork, Phnom Penh", "Heng Rith", "+855 12 333 555", "Father"),
            ("ADM-2026-0008", "Thida", "Meas", "Female", "thida.meas@student.angkoracademy.edu.kh", "+855 98 444 555", "Khan Chamkarmon, Phnom Penh", "Meas Chea", "+855 12 444 666", "Mother")
        };

        int roll = existingStudents.Count + 1;
        bool hasChanges = false;
        foreach (var sData in additionalStudents)
        {
            if (!existingStudents.Any(s => s.AdmissionNumber == sData.Adm))
            {
                hasChanges = true;
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    SchoolId = school.Id,
                    Username = $"student.{sData.First.ToLower()}",
                    Email = sData.Email,
                    PasswordHash = studentPasswordHash,
                    FirstName = sData.First,
                    LastName = sData.Last,
                    Role = UserRole.Student,
                    IsActive = true
                };
                _context.Users.Add(user);

                var newStudent = new Student
                {
                    Id = Guid.NewGuid(),
                    SchoolId = school.Id,
                    UserId = user.Id,
                    AdmissionNumber = sData.Adm,
                    FirstName = sData.First,
                    LastName = sData.Last,
                    DateOfBirth = DateTime.SpecifyKind(new DateTime(2010, 6, 15), DateTimeKind.Utc),
                    Gender = sData.Gender,
                    BloodGroup = "O+",
                    EnrollmentDate = DateTime.SpecifyKind(new DateTime(2026, 9, 1), DateTimeKind.Utc),
                    Status = StudentStatus.Active,
                    Email = sData.Email,
                    PhoneNumber = sData.Phone,
                    Address = sData.Address,
                    CurrentClassSectionId = class10A.Id,
                    EmergencyContactName = sData.EmergencyName,
                    EmergencyContactPhone = sData.EmergencyPhone,
                    EmergencyContactRelationship = sData.Rel
                };
                _context.Students.Add(newStudent);

                _context.StudentEnrollments.Add(new StudentEnrollment
                {
                    Id = Guid.NewGuid(),
                    SchoolId = school.Id,
                    StudentId = newStudent.Id,
                    ClassSectionId = class10A.Id,
                    AcademicYearId = academicYear.Id,
                    RollNumber = roll++,
                    EnrollmentDate = DateTime.UtcNow,
                    Status = EnrollmentStatus.Enrolled
                });
            }
        }

        // Ensure all 8 teachers exist in database
        var existingTeachers = await _context.Teachers.IgnoreQueryFilters().Where(t => t.SchoolId == school.Id).ToListAsync();
        var teacherPasswordHash = _passwordHasher.HashPassword("Teacher@123!");

        var teacherSpecs = new List<(Guid TeacherId, Guid UserId, string EmpNo, string First, string Last, string Email, string Phone, string Qual, string Spec)>
        {
            (Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "EMP-00101", "Dara", "Sok", "dara.sok@angkoracademy.edu.kh", "+855 12 345 678", "Master of Mathematics Education (RUPP)", "Advanced Mathematics & Calculus"),
            (Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "EMP-00102", "Chenda", "Chan", "chenda.chan@angkoracademy.edu.kh", "+855 12 456 789", "Bachelor of Science in Physics (ITC)", "Physics & Applied Science"),
            (Guid.Parse("c0000000-0000-0000-0000-000000000001"), Guid.Parse("c0000001-0000-0000-0000-000000000001"), "EMP-00103", "Sokha", "Mean", "sokha.mean@angkoracademy.edu.kh", "+855 12 567 890", "Master of Khmer Literature (RUPP)", "Classical Khmer Literature & Poetry"),
            (Guid.Parse("c0000000-0000-0000-0000-000000000004"), Guid.Parse("c0000001-0000-0000-0000-000000000004"), "EMP-00104", "Rathana", "Vong", "rathana.vong@angkoracademy.edu.kh", "+855 12 678 901", "Master of Chemical Sciences (ITC)", "General & Organic Chemistry"),
            (Guid.Parse("c0000000-0000-0000-0000-000000000005"), Guid.Parse("c0000001-0000-0000-0000-000000000005"), "EMP-00105", "John", "Smith", "john.smith@angkoracademy.edu.kh", "+855 12 789 012", "MA in Applied Linguistics (Melbourne)", "English Communication & Academic Writing"),
            (Guid.Parse("c0000000-0000-0000-0000-000000000006"), Guid.Parse("c0000001-0000-0000-0000-000000000006"), "EMP-00106", "Kolab", "Pich", "kolab.pich@angkoracademy.edu.kh", "+855 12 890 123", "M.Sc in Biological Sciences (RUPP)", "Cellular Biology & Genetics"),
            (Guid.Parse("c0000000-0000-0000-0000-000000000007"), Guid.Parse("c0000001-0000-0000-0000-000000000007"), "EMP-00107", "Sophal", "Tep", "sophal.tep@angkoracademy.edu.kh", "+855 12 901 234", "Master of Southeast Asian History (RUPP)", "Cambodian History & Civilization"),
            (Guid.Parse("c0000000-0000-0000-0000-000000000008"), Guid.Parse("c0000001-0000-0000-0000-000000000008"), "EMP-00108", "Samnang", "Ly", "samnang.ly@angkoracademy.edu.kh", "+855 12 012 345", "B.Sc in Software Engineering (RUPP)", "Computer Science & ICT")
        };

        foreach (var tSpec in teacherSpecs)
        {
            var t = existingTeachers.FirstOrDefault(x => x.EmployeeNumber == tSpec.EmpNo || (x.FirstName == tSpec.First && x.LastName == tSpec.Last));
            if (t == null)
            {
                var existingUser = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == tSpec.Email);
                var userId = existingUser?.Id ?? tSpec.UserId;
                if (existingUser == null)
                {
                    var user = new User
                    {
                        Id = userId,
                        SchoolId = school.Id,
                        Username = $"teacher.{tSpec.First.ToLower()}",
                        Email = tSpec.Email,
                        PasswordHash = teacherPasswordHash,
                        FirstName = tSpec.First,
                        LastName = tSpec.Last,
                        PhoneNumber = tSpec.Phone,
                        Role = UserRole.Teacher,
                        IsActive = true
                    };
                    _context.Users.Add(user);
                }

                t = new Teacher
                {
                    Id = tSpec.TeacherId,
                    SchoolId = school.Id,
                    UserId = userId,
                    EmployeeNumber = tSpec.EmpNo,
                    FirstName = tSpec.First,
                    LastName = tSpec.Last,
                    Email = tSpec.Email,
                    PhoneNumber = tSpec.Phone,
                    HireDate = DateTime.SpecifyKind(new DateTime(2021, 9, 1), DateTimeKind.Utc),
                    Qualification = tSpec.Qual,
                    Specialization = tSpec.Spec,
                    IsActive = true
                };
                _context.Teachers.Add(t);
                existingTeachers.Add(t);
                hasChanges = true;
            }
        }

        var existingTimetableCount = await _context.TimetableEntries.IgnoreQueryFilters().CountAsync(t => t.ClassSectionId == class10A.Id);
        if (existingTimetableCount < 20)
        {
            var subjects = await _context.Subjects.IgnoreQueryFilters().Where(s => s.SchoolId == school.Id).ToListAsync();

            var math = subjects.FirstOrDefault(s => s.Code == "MATH101") ?? subjects.FirstOrDefault(s => s.Name.Contains("Math"));
            var physics = subjects.FirstOrDefault(s => s.Code == "PHY101") ?? subjects.FirstOrDefault(s => s.Name.Contains("Physics"));
            var khmer = subjects.FirstOrDefault(s => s.Code == "KHM101") ?? subjects.FirstOrDefault(s => s.Name.Contains("Khmer"));
            var english = subjects.FirstOrDefault(s => s.Code == "ENG101") ?? subjects.FirstOrDefault(s => s.Name.Contains("English"));
            var chemistry = subjects.FirstOrDefault(s => s.Code == "CHM101") ?? subjects.FirstOrDefault(s => s.Name.Contains("Chemistry"));
            var biology = subjects.FirstOrDefault(s => s.Code == "BIO101") ?? subjects.FirstOrDefault(s => s.Name.Contains("Biology"));
            var history = subjects.FirstOrDefault(s => s.Code == "HIS101") ?? subjects.FirstOrDefault(s => s.Name.Contains("History"));
            var ict = subjects.FirstOrDefault(s => s.Code == "ICT101") ?? subjects.FirstOrDefault(s => s.Name.Contains("Computer") || s.Name.Contains("ICT"));

            var mathTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Dara") ?? existingTeachers.FirstOrDefault();
            var physicsTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Chenda") ?? existingTeachers.FirstOrDefault();
            var khmerTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Sokha") ?? existingTeachers.FirstOrDefault();
            var chemTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Rathana") ?? existingTeachers.FirstOrDefault();
            var engTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "John") ?? existingTeachers.FirstOrDefault();
            var bioTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Kolab") ?? existingTeachers.FirstOrDefault();
            var histTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Sophal") ?? existingTeachers.FirstOrDefault();
            var ictTeacher = existingTeachers.FirstOrDefault(t => t.FirstName == "Samnang") ?? existingTeachers.FirstOrDefault();

            if (math != null && physics != null && khmer != null && english != null &&
                chemistry != null && biology != null && history != null && ict != null &&
                mathTeacher != null && physicsTeacher != null)
            {
                var sparseEntries = await _context.TimetableEntries.IgnoreQueryFilters().Where(t => t.ClassSectionId == class10A.Id).ToListAsync();
                if (sparseEntries.Any())
                {
                    _context.TimetableEntries.RemoveRange(sparseEntries);
                }

                _context.TimetableEntries.AddRange(
                    // Monday
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = mathTeacher.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = physicsTeacher.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Physics Lab" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = (khmerTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = (engTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Monday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Room 201" },

                    // Tuesday
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = chemistry.Id, TeacherId = (chemTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Chemistry Lab" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = biology.Id, TeacherId = (bioTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Biology Lab" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = mathTeacher.Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = history.Id, TeacherId = (histTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Tuesday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Room 201" },

                    // Wednesday
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = ict.Id, TeacherId = (ictTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Computer Lab 1" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = (khmerTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = (engTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = physicsTeacher.Id, DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Physics Lab" },

                    // Thursday
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = math.Id, TeacherId = mathTeacher.Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = chemistry.Id, TeacherId = (chemTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Chemistry Lab" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = history.Id, TeacherId = (histTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = biology.Id, TeacherId = (bioTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Thursday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Biology Lab" },

                    // Friday
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = khmer.Id, TeacherId = (khmerTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = english.Id, TeacherId = (engTeacher ?? physicsTeacher).Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(11, 30, 0), RoomNumber = "Room 201" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = ict.Id, TeacherId = (ictTeacher ?? mathTeacher).Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(13, 0, 0), EndTime = new TimeSpan(14, 30, 0), RoomNumber = "Computer Lab 1" },
                    new TimetableEntry { Id = Guid.NewGuid(), SchoolId = school.Id, ClassSectionId = class10A.Id, SubjectId = physics.Id, TeacherId = physicsTeacher.Id, DayOfWeek = DayOfWeek.Friday, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(16, 30, 0), RoomNumber = "Physics Lab" }
                );
                hasChanges = true;
            }
        }

        // Ensure Midterm Exam and 8 Assessment Components exist for Class 10-A
        var exam = await _context.Exams.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.SchoolId == school.Id && e.Name.Contains("Midterm"));
        if (exam == null)
        {
            var semester1 = await _context.Semesters.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SchoolId == school.Id && s.IsCurrent)
                ?? await _context.Semesters.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SchoolId == school.Id);
            if (semester1 != null)
            {
                exam = new Exam
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
                hasChanges = true;
            }
        }

        if (exam != null)
        {
            var subjects = await _context.Subjects.IgnoreQueryFilters().Where(s => s.SchoolId == school.Id).ToListAsync();
            var existingComponents = await _context.AssessmentComponents.IgnoreQueryFilters()
                .Where(c => c.ClassSectionId == class10A.Id && c.ExamId == exam.Id)
                .ToListAsync();

            var subjectSpecs = new List<(string Code, string Keyword, string CompName)>
            {
                ("KHM101", "Khmer", "Khmer Composition Exam"),
                ("MATH101", "Math", "Midterm Written Exam"),
                ("PHY101", "Physics", "Physics Practical & Theory"),
                ("CHM101", "Chemistry", "Chemistry Midterm Exam"),
                ("ENG101", "English", "English Listening & Essay"),
                ("BIO101", "Biology", "Biology Laboratory & Theory"),
                ("HIS101", "History", "History Midterm Essay"),
                ("ICT101", "ICT", "ICT Practical Project")
            };

            var activeComponents = new List<AssessmentComponent>();
            foreach (var spec in subjectSpecs)
            {
                var subj = subjects.FirstOrDefault(s => s.Code == spec.Code)
                    ?? subjects.FirstOrDefault(s => s.Name.Contains(spec.Keyword));
                if (subj == null) continue;

                var comp = existingComponents.FirstOrDefault(c => c.SubjectId == subj.Id);
                if (comp == null)
                {
                    comp = new AssessmentComponent
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = school.Id,
                        ExamId = exam.Id,
                        SubjectId = subj.Id,
                        ClassSectionId = class10A.Id,
                        Name = spec.CompName,
                        MaxScore = 100m,
                        WeightPercentage = 40m
                    };
                    _context.AssessmentComponents.Add(comp);
                    hasChanges = true;
                }
                activeComponents.Add(comp);
            }

            // Ensure all 8 Class 10-A students have recorded grades across all 8 assessment components
            var allClassStudents = await _context.Students.IgnoreQueryFilters()
                .Where(s => s.SchoolId == school.Id && s.CurrentClassSectionId == class10A.Id)
                .ToListAsync();

            var adminUser = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.SchoolId == school.Id && u.Role == UserRole.SchoolAdmin);
            var graderId = adminUser?.Id ?? Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            var existingGrades = await _context.StudentGrades.IgnoreQueryFilters()
                .Where(g => g.SchoolId == school.Id)
                .ToListAsync();

            var sampleScores = new Dictionary<string, (decimal Score, string Remarks)>
            {
                { "ADM-2026-0001", (94.0m, "Outstanding analytical ability and class participation") },
                { "ADM-2026-0002", (91.5m, "Consistent academic excellence and thorough preparation") },
                { "ADM-2026-0003", (87.5m, "Very good subject mastery and diligent coursework") },
                { "ADM-2026-0004", (86.0m, "Strong critical understanding and active learning") },
                { "ADM-2026-0005", (89.0m, "Demonstrated high competence and excellent homework") },
                { "ADM-2026-0006", (90.5m, "Clear problem-solving logic and neat assignments") },
                { "ADM-2026-0007", (88.0m, "Solid conceptual understanding and good effort") },
                { "ADM-2026-0008", (92.0m, "Superior exam performance and high exam score") }
            };

            foreach (var comp in activeComponents)
            {
                foreach (var st in allClassStudents)
                {
                    bool gradeExists = existingGrades.Any(g => g.AssessmentComponentId == comp.Id && g.StudentId == st.Id);
                    if (!gradeExists)
                    {
                        var (baseScore, remarks) = sampleScores.TryGetValue(st.AdmissionNumber, out var sc)
                            ? sc
                            : (88.0m, "Good academic performance");

                        _context.StudentGrades.Add(new StudentGrade
                        {
                            Id = Guid.NewGuid(),
                            SchoolId = school.Id,
                            AssessmentComponentId = comp.Id,
                            StudentId = st.Id,
                            ScoreObtained = baseScore,
                            Remarks = remarks,
                            GradedByUserId = graderId,
                            GradedAtUtc = DateTime.UtcNow
                        });
                        hasChanges = true;
                    }
                }
            }
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully verified and synchronized full roster, 8-subject timetable, and class 10-A grades.");
        }
    }
}
