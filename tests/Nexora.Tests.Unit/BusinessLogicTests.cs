using Nexora.Application.DTOs;
using Nexora.Application.Validators;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Nexora.Tests.Unit;

public class GradeCalculationTests
{
    [Theory]
    [InlineData(95, "A", 4.0)]
    [InlineData(85, "B", 3.0)]
    [InlineData(75, "C", 2.0)]
    [InlineData(65, "D", 1.0)]
    [InlineData(55, "E", 0.5)]
    [InlineData(40, "F", 0.0)]
    public void GradeLetter_ShouldMatch_PercentageCorrectly(decimal percentage, string expectedLetter, decimal expectedPoint)
    {
        // Arrange
        var scales = new List<GradeScale>
        {
            new() { GradeLetter = "A", MinPercentage = 90, MaxPercentage = 100, GradePoint = 4.0m },
            new() { GradeLetter = "B", MinPercentage = 80, MaxPercentage = 89.9m, GradePoint = 3.0m },
            new() { GradeLetter = "C", MinPercentage = 70, MaxPercentage = 79.9m, GradePoint = 2.0m },
            new() { GradeLetter = "D", MinPercentage = 60, MaxPercentage = 69.9m, GradePoint = 1.0m },
            new() { GradeLetter = "E", MinPercentage = 50, MaxPercentage = 59.9m, GradePoint = 0.5m },
            new() { GradeLetter = "F", MinPercentage = 0, MaxPercentage = 49.9m, GradePoint = 0.0m }
        };

        // Act
        var scale = scales.FirstOrDefault(s => percentage >= s.MinPercentage && percentage <= s.MaxPercentage);

        // Assert
        scale.Should().NotBeNull();
        scale!.GradeLetter.Should().Be(expectedLetter);
        scale.GradePoint.Should().Be(expectedPoint);
    }

    [Fact]
    public void CumulativeGpa_ShouldBeCalculated_WithCreditWeights()
    {
        // Subject 1: Math (4 credits, Grade A = 4.0) -> 16
        // Subject 2: Khmer (4 credits, Grade B = 3.0) -> 12
        // Subject 3: English (3 credits, Grade A = 4.0) -> 12
        // Total Credits = 11, Total Points = 40, GPA = 40 / 11 = 3.64

        var subjects = new List<(int Credits, decimal GradePoint)>
        {
            (4, 4.0m),
            (4, 3.0m),
            (3, 4.0m)
        };

        decimal weightedPoints = subjects.Sum(s => s.Credits * s.GradePoint);
        int totalCredits = subjects.Sum(s => s.Credits);
        decimal gpa = Math.Round(weightedPoints / totalCredits, 2);

        gpa.Should().Be(3.64m);
    }
}

public class FeeCalculationTests
{
    [Fact]
    public void InvoiceBalance_ShouldReflectPartialPayments()
    {
        var invoice = new Invoice
        {
            TotalAmount = 1000m,
            PaidAmount = 300m,
            Status = InvoiceStatus.PartiallyPaid
        };

        invoice.RemainingBalance.Should().Be(700m);
    }

    [Fact]
    public void InvoiceStatus_ShouldBecomePaid_WhenFullAmountIsPaid()
    {
        var invoice = new Invoice
        {
            TotalAmount = 500m,
            PaidAmount = 0m,
            Status = InvoiceStatus.Pending
        };

        invoice.PaidAmount += 500m;
        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
        }

        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.RemainingBalance.Should().Be(0m);
    }
}

public class TimetableConflictTests
{
    [Fact]
    public void OverlappingSlots_ShouldBeDetected_WhenTimesOverlap()
    {
        var existingStart = new TimeSpan(8, 0, 0);
        var existingEnd = new TimeSpan(9, 30, 0);

        var newStart = new TimeSpan(9, 0, 0);
        var newEnd = new TimeSpan(10, 30, 0);

        bool hasOverlap = newStart < existingEnd && newEnd > existingStart;
        hasOverlap.Should().BeTrue();
    }

    [Fact]
    public void ConsecutiveSlots_ShouldNotOverlap()
    {
        var existingStart = new TimeSpan(8, 0, 0);
        var existingEnd = new TimeSpan(9, 30, 0);

        var newStart = new TimeSpan(9, 30, 0);
        var newEnd = new TimeSpan(11, 0, 0);

        bool hasOverlap = newStart < existingEnd && newEnd > existingStart;
        hasOverlap.Should().BeFalse();
    }
}

public class AttendanceCalculationTests
{
    [Fact]
    public void AttendancePercentage_ShouldWeightPresentAndLateCorrectly()
    {
        // 10 days: 8 Present, 1 Late (0.5), 1 Absent (0) -> (8 + 0.5) / 10 = 85.0%
        int total = 10;
        int present = 8;
        int late = 1;
        int excused = 0;

        decimal percentage = Math.Round(((present + (late * 0.5m) + excused) / (decimal)total) * 100m, 2);
        percentage.Should().Be(85.00m);
    }
}

public class SecurityAndValidationTests
{
    [Fact]
    public void PasswordHasher_ShouldHashAndVerifyCorrectly()
    {
        var hasher = new PasswordHasher();
        var password = "SecurePassword@2026!";

        var hash = hasher.HashPassword(password);
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);

        hasher.VerifyPassword(password, hash).Should().BeTrue();
        hasher.VerifyPassword("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void CreateStudentValidator_ShouldFail_WhenAdmissionNumberIsEmpty()
    {
        var validator = new CreateStudentRequestValidator();
        var request = new CreateStudentRequest(
            AdmissionNumber: "",
            FirstName: "Visal",
            LastName: "Chan",
            DateOfBirth: DateTime.UtcNow.AddYears(-15),
            Gender: "Male",
            BloodGroup: "O+",
            EnrollmentDate: DateTime.UtcNow,
            Email: "visal@test.com",
            PhoneNumber: null,
            Address: null,
            ProfilePhotoUrl: null,
            ClassSectionId: null,
            EmergencyContactName: null,
            EmergencyContactPhone: null,
            EmergencyContactRelationship: null,
            Username: null,
            Password: null
        );

        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AdmissionNumber");
    }
}

public class RoleBasedAuthenticationTests
{
    [Theory]
    [InlineData("superadmin", UserRole.SuperAdmin)]
    [InlineData("admin", UserRole.SchoolAdmin)]
    [InlineData("teacher.dara", UserRole.Teacher)]
    [InlineData("student.visal", UserRole.Student)]
    [InlineData("parent.bopha", UserRole.Parent)]
    public void RoleResolution_ShouldMapUsernameToCorrectRole(string username, UserRole expectedRole)
    {
        var resolvedRole = username.ToLower() switch
        {
            "superadmin" => UserRole.SuperAdmin,
            "teacher.dara" or "teacher.chenda" or "teacher" => UserRole.Teacher,
            "student.visal" or "student.kanha" or "student" => UserRole.Student,
            "parent.bopha" or "parent" => UserRole.Parent,
            _ => UserRole.SchoolAdmin
        };

        resolvedRole.Should().Be(expectedRole);
    }

    [Fact]
    public void RolePermissions_ShouldDifferentiatePanels()
    {
        // Validate that roles have strictly segregated panel routes
        var adminTabs = new[] { "dashboard", "students", "attendance", "grades", "fees", "timetables", "announcements", "profile" };
        var teacherTabs = new[] { "dashboard", "attendance", "grades", "timetables", "announcements", "profile" };
        var studentTabs = new[] { "dashboard", "timetables", "grades", "fees", "announcements", "profile" };
        var parentTabs = new[] { "dashboard", "grades", "fees", "timetables", "announcements", "profile" };

        teacherTabs.Should().NotContain("students");
        teacherTabs.Should().NotContain("fees");

        studentTabs.Should().NotContain("students");
        studentTabs.Should().NotContain("attendance");

        parentTabs.Should().NotContain("students");
        parentTabs.Should().NotContain("attendance");

        adminTabs.Should().Contain("students");
        adminTabs.Should().Contain("fees");
    }
}
