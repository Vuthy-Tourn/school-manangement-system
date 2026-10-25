using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexora.Api.Controllers;

[Authorize]
public class AcademicsController : BaseApiController
{
    private readonly IAcademicService _academicService;

    public AcademicsController(IAcademicService academicService)
    {
        _academicService = academicService;
    }

    [HttpGet("years")]
    public async Task<ActionResult<ApiResponse<List<AcademicYearDto>>>> GetAcademicYears(CancellationToken ct)
    {
        return HandleResult(await _academicService.GetAcademicYearsAsync(ct));
    }

    [HttpPost("years")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<AcademicYearDto>>> CreateAcademicYear([FromBody] CreateAcademicYearRequest request, CancellationToken ct)
    {
        return HandleResult(await _academicService.CreateAcademicYearAsync(request, ct));
    }

    [HttpGet("years/{yearId:guid}/semesters")]
    public async Task<ActionResult<ApiResponse<List<SemesterDto>>>> GetSemesters(Guid yearId, CancellationToken ct)
    {
        return HandleResult(await _academicService.GetSemestersAsync(yearId, ct));
    }

    [HttpGet("grade-levels")]
    public async Task<ActionResult<ApiResponse<List<GradeLevelDto>>>> GetGradeLevels(CancellationToken ct)
    {
        return HandleResult(await _academicService.GetGradeLevelsAsync(ct));
    }

    [HttpGet("classes")]
    public async Task<ActionResult<ApiResponse<List<ClassSectionDto>>>> GetClassSections([FromQuery] Guid? academicYearId, CancellationToken ct)
    {
        return HandleResult(await _academicService.GetClassSectionsAsync(academicYearId, ct));
    }

    [HttpPost("classes")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<ClassSectionDto>>> CreateClassSection([FromBody] CreateClassSectionRequest request, CancellationToken ct)
    {
        return HandleResult(await _academicService.CreateClassSectionAsync(request, ct));
    }

    [HttpGet("subjects")]
    public async Task<ActionResult<ApiResponse<List<SubjectDto>>>> GetSubjects(CancellationToken ct)
    {
        return HandleResult(await _academicService.GetSubjectsAsync(ct));
    }

    [HttpPost("subjects")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<SubjectDto>>> CreateSubject([FromBody] CreateSubjectRequest request, CancellationToken ct)
    {
        return HandleResult(await _academicService.CreateSubjectAsync(request, ct));
    }

    [HttpPost("assign-teacher")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> AssignTeacher([FromBody] AssignTeacherSubjectRequest request, CancellationToken ct)
    {
        return HandleResult(await _academicService.AssignTeacherToSubjectAsync(request, ct));
    }

    [HttpPost("enroll-student")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<StudentEnrollmentDto>>> EnrollStudent([FromBody] EnrollStudentRequest request, CancellationToken ct)
    {
        return HandleResult(await _academicService.EnrollStudentAsync(request, ct));
    }

    [HttpPost("promote-student")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> PromoteStudent([FromBody] PromoteStudentRequest request, CancellationToken ct)
    {
        return HandleResult(await _academicService.PromoteStudentAsync(request, ct));
    }
}

[Authorize]
public class TimetablesController : BaseApiController
{
    private readonly ITimetableService _timetableService;

    public TimetablesController(ITimetableService timetableService)
    {
        _timetableService = timetableService;
    }

    [HttpGet("class/{classId:guid}")]
    public async Task<ActionResult<ApiResponse<List<TimetableEntryDto>>>> GetClassTimetable(Guid classId, CancellationToken ct)
    {
        return HandleResult(await _timetableService.GetClassTimetableAsync(classId, ct));
    }

    [HttpGet("teacher/{teacherId:guid}")]
    public async Task<ActionResult<ApiResponse<List<TimetableEntryDto>>>> GetTeacherTimetable(Guid teacherId, CancellationToken ct)
    {
        return HandleResult(await _timetableService.GetTeacherTimetableAsync(teacherId, ct));
    }

    [HttpGet("student/{studentId:guid}")]
    public async Task<ActionResult<ApiResponse<List<TimetableEntryDto>>>> GetStudentTimetable(Guid studentId, CancellationToken ct)
    {
        return HandleResult(await _timetableService.GetStudentTimetableAsync(studentId, ct));
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<TimetableEntryDto>>> CreateEntry([FromBody] CreateTimetableEntryRequest request, CancellationToken ct)
    {
        return HandleResult(await _timetableService.CreateEntryAsync(request, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEntry(Guid id, CancellationToken ct)
    {
        return HandleResult(await _timetableService.DeleteEntryAsync(id, ct));
    }
}
