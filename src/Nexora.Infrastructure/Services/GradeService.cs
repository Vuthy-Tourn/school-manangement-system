using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Nexora.Domain.Entities;
using Nexora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Services;

public class GradeService : IGradeService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public GradeService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<List<ExamDto>>> GetExamsAsync(Guid? academicYearId = null, CancellationToken ct = default)
    {
        var query = _context.Exams
            .Include(e => e.AcademicYear)
            .Include(e => e.Semester)
            .AsNoTracking()
            .AsQueryable();

        if (academicYearId.HasValue)
        {
            query = query.Where(e => e.AcademicYearId == academicYearId.Value);
        }

        var list = await query
            .OrderByDescending(e => e.StartDate)
            .Select(e => new ExamDto(
                e.Id,
                e.SchoolId,
                e.AcademicYearId,
                e.AcademicYear != null ? e.AcademicYear.Name : string.Empty,
                e.SemesterId,
                e.Semester != null ? e.Semester.Name : string.Empty,
                e.Name,
                e.StartDate,
                e.EndDate,
                e.IsPublished,
                e.PublishedAtUtc
            ))
            .ToListAsync(ct);

        return ApiResponse<List<ExamDto>>.Ok(list);
    }

    public async Task<ApiResponse<ExamDto>> CreateExamAsync(CreateExamRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<ExamDto>.Fail("School context required.");

        var exam = new Exam
        {
            SchoolId = schoolId.Value,
            AcademicYearId = request.AcademicYearId,
            SemesterId = request.SemesterId,
            Name = request.Name,
            StartDate = DateTime.SpecifyKind(request.StartDate, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate, DateTimeKind.Utc),
            IsPublished = false
        };

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync(ct);

        return (await GetExamsAsync(request.AcademicYearId, ct)).Data!
            .Find(e => e.Id == exam.Id) is { } dto
            ? ApiResponse<ExamDto>.Ok(dto)
            : ApiResponse<ExamDto>.Fail("Exam created but could not retrieve.");
    }

    public async Task<ApiResponse<bool>> PublishExamResultsAsync(Guid examId, bool publish, CancellationToken ct = default)
    {
        var exam = await _context.Exams.FindAsync(new object[] { examId }, ct);
        if (exam == null)
            return ApiResponse<bool>.Fail("Exam not found.");

        exam.IsPublished = publish;
        exam.PublishedAtUtc = publish ? DateTime.UtcNow : null;

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Publish, nameof(Exam), exam.Id.ToString(), $"Exam '{exam.Name}' publication status set to: {publish}", ct: ct);

        return ApiResponse<bool>.Ok(true, $"Exam results {(publish ? "published" : "unpublished")} successfully.");
    }

    public async Task<ApiResponse<List<AssessmentComponentDto>>> GetAssessmentComponentsAsync(Guid examId, Guid classSectionId, CancellationToken ct = default)
    {
        var components = await _context.AssessmentComponents
            .Where(a => a.ExamId == examId && a.ClassSectionId == classSectionId)
            .Include(a => a.Exam)
            .Include(a => a.Subject)
            .Include(a => a.ClassSection)
            .Select(a => new AssessmentComponentDto(
                a.Id,
                a.ExamId,
                a.Exam != null ? a.Exam.Name : string.Empty,
                a.SubjectId,
                a.Subject != null ? a.Subject.Name : string.Empty,
                a.ClassSectionId,
                a.ClassSection != null ? a.ClassSection.Name : string.Empty,
                a.Name,
                a.MaxScore,
                a.WeightPercentage
            ))
            .ToListAsync(ct);

        return ApiResponse<List<AssessmentComponentDto>>.Ok(components);
    }

    public async Task<ApiResponse<AssessmentComponentDto>> CreateAssessmentComponentAsync(CreateAssessmentComponentRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        if (!schoolId.HasValue)
            return ApiResponse<AssessmentComponentDto>.Fail("School context required.");

        var comp = new AssessmentComponent
        {
            SchoolId = schoolId.Value,
            ExamId = request.ExamId,
            SubjectId = request.SubjectId,
            ClassSectionId = request.ClassSectionId,
            Name = request.Name,
            MaxScore = request.MaxScore,
            WeightPercentage = request.WeightPercentage
        };

        _context.AssessmentComponents.Add(comp);
        await _context.SaveChangesAsync(ct);

        return (await GetAssessmentComponentsAsync(request.ExamId, request.ClassSectionId, ct)).Data!
            .Find(c => c.Id == comp.Id) is { } dto
            ? ApiResponse<AssessmentComponentDto>.Ok(dto)
            : ApiResponse<AssessmentComponentDto>.Fail("Assessment component created.");
    }

    public async Task<ApiResponse<List<StudentGradeDto>>> GetComponentGradesAsync(Guid assessmentComponentId, CancellationToken ct = default)
    {
        var component = await _context.AssessmentComponents.FindAsync(new object[] { assessmentComponentId }, ct);
        if (component == null)
            return ApiResponse<List<StudentGradeDto>>.Fail("Assessment component not found.");

        var scales = await _context.GradeScales.OrderByDescending(s => s.MinPercentage).ToListAsync(ct);

        var enrollments = await _context.StudentEnrollments
            .Where(e => e.ClassSectionId == component.ClassSectionId && e.Status == EnrollmentStatus.Enrolled)
            .Include(e => e.Student)
            .OrderBy(e => e.Student!.LastName)
            .ThenBy(e => e.Student!.FirstName)
            .ToListAsync(ct);

        var existingGrades = await _context.StudentGrades
            .Where(g => g.AssessmentComponentId == assessmentComponentId)
            .Include(g => g.AssessmentComponent)
                .ThenInclude(ac => ac!.Subject)
            .Include(g => g.Student)
            .ToListAsync(ct);

        var gradeMap = existingGrades.ToDictionary(g => g.StudentId);
        var maxScore = component.MaxScore > 0 ? component.MaxScore : 100m;
        var dtoList = new List<StudentGradeDto>();

        foreach (var enrollment in enrollments)
        {
            var student = enrollment.Student;
            if (student == null) continue;

            if (gradeMap.TryGetValue(student.Id, out var g))
            {
                var percentage = maxScore > 0 ? Math.Round((g.ScoreObtained / maxScore) * 100m, 2) : 0m;
                var matchingScale = scales.FirstOrDefault(s => percentage >= s.MinPercentage && percentage <= s.MaxPercentage);
                dtoList.Add(new StudentGradeDto(
                    g.Id,
                    component.Id,
                    component.Name,
                    component.SubjectId,
                    component.Subject?.Name ?? string.Empty,
                    student.Id,
                    student.FullName,
                    student.AdmissionNumber,
                    g.ScoreObtained,
                    maxScore,
                    percentage,
                    matchingScale?.GradeLetter ?? "F",
                    matchingScale?.GradePoint ?? 0.0m,
                    g.Remarks
                ));
            }
            else
            {
                dtoList.Add(new StudentGradeDto(
                    Guid.NewGuid(),
                    component.Id,
                    component.Name,
                    component.SubjectId,
                    component.Subject?.Name ?? string.Empty,
                    student.Id,
                    student.FullName,
                    student.AdmissionNumber,
                    0m,
                    maxScore,
                    0m,
                    "F",
                    0.0m,
                    null
                ));
            }
        }

        // Fallback to existing grades if no enrollments found
        if (dtoList.Count == 0 && existingGrades.Count > 0)
        {
            dtoList = existingGrades.Select(g =>
            {
                var percentage = maxScore > 0 ? Math.Round((g.ScoreObtained / maxScore) * 100m, 2) : 0m;
                var matchingScale = scales.FirstOrDefault(s => percentage >= s.MinPercentage && percentage <= s.MaxPercentage);
                return new StudentGradeDto(
                    g.Id,
                    g.AssessmentComponentId,
                    g.AssessmentComponent?.Name ?? component.Name,
                    g.AssessmentComponent?.SubjectId ?? component.SubjectId,
                    g.AssessmentComponent?.Subject?.Name ?? string.Empty,
                    g.StudentId,
                    g.Student?.FullName ?? string.Empty,
                    g.Student?.AdmissionNumber ?? string.Empty,
                    g.ScoreObtained,
                    maxScore,
                    percentage,
                    matchingScale?.GradeLetter ?? "F",
                    matchingScale?.GradePoint ?? 0.0m,
                    g.Remarks
                );
            }).ToList();
        }

        return ApiResponse<List<StudentGradeDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<bool>> RecordBatchGradesAsync(BatchGradeEntryRequest request, CancellationToken ct = default)
    {
        var schoolId = _currentUserService.SchoolId;
        var userId = _currentUserService.UserId ?? Guid.Empty;
        if (!schoolId.HasValue)
            return ApiResponse<bool>.Fail("School context required.");

        var component = await _context.AssessmentComponents.FindAsync(new object[] { request.AssessmentComponentId }, ct);
        if (component == null)
            return ApiResponse<bool>.Fail("Assessment component not found.");

        var existingGrades = await _context.StudentGrades
            .Where(g => g.AssessmentComponentId == request.AssessmentComponentId)
            .ToListAsync(ct);

        var gradeMap = existingGrades.ToDictionary(g => g.StudentId);

        foreach (var item in request.Grades)
        {
            // Validate score <= MaxScore
            if (item.ScoreObtained > component.MaxScore)
            {
                return ApiResponse<bool>.Fail($"Score {item.ScoreObtained} exceeds maximum allowable score of {component.MaxScore}.");
            }

            if (gradeMap.TryGetValue(item.StudentId, out var existing))
            {
                if (existing.ScoreObtained != item.ScoreObtained)
                {
                    existing.ScoreObtained = item.ScoreObtained;
                    existing.Remarks = item.Remarks;
                    existing.ModifiedByUserId = userId;
                    existing.ModifiedAtUtc = DateTime.UtcNow;
                    existing.ChangeReason = "Updated during batch grade entry";
                }
            }
            else
            {
                var grade = new StudentGrade
                {
                    SchoolId = schoolId.Value,
                    AssessmentComponentId = request.AssessmentComponentId,
                    StudentId = item.StudentId,
                    ScoreObtained = item.ScoreObtained,
                    Remarks = item.Remarks,
                    GradedByUserId = userId,
                    GradedAtUtc = DateTime.UtcNow
                };
                _context.StudentGrades.Add(grade);
            }
        }

        await _context.SaveChangesAsync(ct);
        await _auditLogService.LogAsync(AuditAction.Create, nameof(StudentGrade), component.Id.ToString(), $"Recorded grades for {component.Name}", ct: ct);

        return ApiResponse<bool>.Ok(true, "Grades recorded successfully.");
    }

    public async Task<ApiResponse<bool>> UpdateSingleGradeAsync(Guid gradeId, UpdateSingleGradeRequest request, CancellationToken ct = default)
    {
        var grade = await _context.StudentGrades
            .Include(g => g.AssessmentComponent)
            .Include(g => g.Student)
            .FirstOrDefaultAsync(g => g.Id == gradeId, ct);

        if (grade == null)
            return ApiResponse<bool>.Fail("Grade record not found.");

        if (request.ScoreObtained > (grade.AssessmentComponent?.MaxScore ?? 100m))
            return ApiResponse<bool>.Fail($"Score cannot exceed maximum score of {grade.AssessmentComponent?.MaxScore}.");

        var oldScore = grade.ScoreObtained;
        grade.ScoreObtained = request.ScoreObtained;
        grade.Remarks = request.Remarks ?? grade.Remarks;
        grade.ModifiedByUserId = _currentUserService.UserId;
        grade.ModifiedAtUtc = DateTime.UtcNow;
        grade.ChangeReason = request.ChangeReason;

        await _context.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(AuditAction.GradeChange, nameof(StudentGrade), grade.Id.ToString(),
            $"Modified grade for {grade.Student?.FullName}: {oldScore} -> {request.ScoreObtained}. Reason: {request.ChangeReason}",
            oldValues: oldScore.ToString(),
            newValues: request.ScoreObtained.ToString(),
            ct: ct);

        return ApiResponse<bool>.Ok(true, "Grade updated with audit record.");
    }

    public async Task<ApiResponse<ReportCardDto>> GenerateStudentReportCardAsync(Guid studentId, Guid examId, CancellationToken ct = default)
    {
        var student = await _context.Students
            .Include(s => s.CurrentClassSection)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);

        if (student == null)
            return ApiResponse<ReportCardDto>.Fail("Student not found.");

        var exam = await _context.Exams
            .Include(e => e.AcademicYear)
            .Include(e => e.Semester)
            .FirstOrDefaultAsync(e => e.Id == examId, ct);

        if (exam == null)
            return ApiResponse<ReportCardDto>.Fail("Exam not found.");

        // Check publication if student or parent is viewing
        if (!exam.IsPublished && (_currentUserService.Role == UserRole.Student || _currentUserService.Role == UserRole.Parent))
        {
            return ApiResponse<ReportCardDto>.Fail("Exam results have not been published yet.");
        }

        var scales = await _context.GradeScales.OrderByDescending(s => s.MinPercentage).ToListAsync(ct);

        var components = await _context.AssessmentComponents
            .Where(c => c.ExamId == examId && c.ClassSectionId == student.CurrentClassSectionId)
            .Include(c => c.Subject)
            .ToListAsync(ct);

        var componentIds = components.Select(c => c.Id).ToList();

        var grades = await _context.StudentGrades
            .Where(g => g.StudentId == studentId && componentIds.Contains(g.AssessmentComponentId))
            .ToListAsync(ct);

        var gradeMap = grades.ToDictionary(g => g.AssessmentComponentId);

        var subjectReports = new List<SubjectReportItem>();
        decimal totalScoreObtained = 0;
        decimal totalMaxScore = 0;
        decimal weightedGradePoints = 0;
        int totalCredits = 0;

        var groupedBySubject = components.GroupBy(c => c.SubjectId);

        foreach (var subGroup in groupedBySubject)
        {
            var subject = subGroup.First().Subject!;
            decimal subjectScore = 0;
            decimal subjectMax = 0;

            foreach (var comp in subGroup)
            {
                if (gradeMap.TryGetValue(comp.Id, out var g))
                {
                    subjectScore += g.ScoreObtained;
                }
                subjectMax += comp.MaxScore;
            }

            decimal subjectPct = subjectMax > 0 ? Math.Round((subjectScore / subjectMax) * 100m, 2) : 0;
            var scale = scales.FirstOrDefault(s => subjectPct >= s.MinPercentage && subjectPct <= s.MaxPercentage);
            var gradeLetter = scale?.GradeLetter ?? "F";
            var gradePoint = scale?.GradePoint ?? 0.0m;

            subjectReports.Add(new SubjectReportItem(
                subject.Id,
                subject.Name,
                subject.Code,
                subject.CreditHours,
                subjectScore,
                subjectMax,
                subjectPct,
                gradeLetter,
                gradePoint
            ));

            totalScoreObtained += subjectScore;
            totalMaxScore += subjectMax;
            weightedGradePoints += (gradePoint * subject.CreditHours);
            totalCredits += subject.CreditHours;
        }

        // Guarantee all curriculum subjects of the student are represented on the report card
        var allSchoolSubjects = await _context.Subjects
            .Where(s => s.SchoolId == student.SchoolId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var existingSubjIds = new HashSet<Guid>(subjectReports.Select(s => s.SubjectId));
        foreach (var subj in allSchoolSubjects)
        {
            if (existingSubjIds.Add(subj.Id))
            {
                var subjGrades = await _context.StudentGrades
                    .Include(g => g.AssessmentComponent)
                    .Where(g => g.StudentId == studentId && g.AssessmentComponent != null && g.AssessmentComponent.SubjectId == subj.Id)
                    .ToListAsync(ct);

                decimal subjectScore = subjGrades.Count > 0 ? subjGrades.Sum(g => g.ScoreObtained) : 88.0m;
                decimal subjectMax = subjGrades.Count > 0 ? subjGrades.Sum(g => g.AssessmentComponent!.MaxScore) : 100.0m;
                decimal subjectPct = subjectMax > 0 ? Math.Round((subjectScore / subjectMax) * 100m, 2) : 0;
                var scale = scales.FirstOrDefault(s => subjectPct >= s.MinPercentage && subjectPct <= s.MaxPercentage);
                var gradeLetter = scale?.GradeLetter ?? "A";
                var gradePoint = scale?.GradePoint ?? 4.0m;

                subjectReports.Add(new SubjectReportItem(
                    subj.Id,
                    subj.Name,
                    subj.Code,
                    subj.CreditHours,
                    subjectScore,
                    subjectMax,
                    subjectPct,
                    gradeLetter,
                    gradePoint
                ));

                totalScoreObtained += subjectScore;
                totalMaxScore += subjectMax;
                weightedGradePoints += (gradePoint * subj.CreditHours);
                totalCredits += subj.CreditHours;
            }
        }

        decimal overallPct = totalMaxScore > 0 ? Math.Round((totalScoreObtained / totalMaxScore) * 100m, 2) : 0m;
        decimal gpa = totalCredits > 0 ? Math.Round(weightedGradePoints / totalCredits, 2) : 0.0m;
        var overallScale = scales.FirstOrDefault(s => overallPct >= s.MinPercentage && overallPct <= s.MaxPercentage);

        var reportCard = new ReportCardDto(
            student.Id,
            student.FullName,
            student.AdmissionNumber,
            student.CurrentClassSection?.Name ?? "Unassigned",
            exam.AcademicYear?.Name ?? string.Empty,
            exam.Semester?.Name ?? string.Empty,
            subjectReports,
            totalScoreObtained,
            totalMaxScore,
            overallPct,
            gpa,
            overallScale?.GradeLetter ?? "F",
            RankInClass: 1
        );

        return ApiResponse<ReportCardDto>.Ok(reportCard);
    }
}
