using EduManage.Application.Common;
using EduManage.Application.DTOs;
using EduManage.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManage.Api.Controllers;

[Authorize]
public class StudentsController : BaseApiController
{
    private readonly IStudentService _studentService;

    public StudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResult<StudentDto>>>> GetStudents([FromQuery] StudentFilter filter, CancellationToken ct)
    {
        var result = await _studentService.GetStudentsAsync(filter, ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StudentDto>>> GetStudent(Guid id, CancellationToken ct)
    {
        var result = await _studentService.GetStudentByIdAsync(id, ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<StudentDto>>> CreateStudent([FromBody] CreateStudentRequest request, CancellationToken ct)
    {
        var result = await _studentService.CreateStudentAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<StudentDto>>> UpdateStudent(Guid id, [FromBody] UpdateStudentRequest request, CancellationToken ct)
    {
        var result = await _studentService.UpdateStudentAsync(id, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeactivateStudent(Guid id, CancellationToken ct)
    {
        var result = await _studentService.DeactivateStudentAsync(id, ct);
        return HandleResult(result);
    }

    [HttpPost("link-guardian")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> LinkGuardian([FromBody] LinkStudentRequest request, CancellationToken ct)
    {
        var result = await _studentService.LinkParentGuardianAsync(request, ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}/guardians")]
    public async Task<ActionResult<ApiResponse<List<ParentGuardianDto>>>> GetStudentGuardians(Guid id, CancellationToken ct)
    {
        var result = await _studentService.GetStudentGuardiansAsync(id, ct);
        return HandleResult(result);
    }
}

[Authorize]
public class TeachersController : BaseApiController
{
    private readonly ITeacherService _teacherService;

    public TeachersController(ITeacherService teacherService)
    {
        _teacherService = teacherService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TeacherDto>>>> GetTeachers(CancellationToken ct)
    {
        var result = await _teacherService.GetTeachersAsync(ct);
        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TeacherDto>>> GetTeacher(Guid id, CancellationToken ct)
    {
        var result = await _teacherService.GetTeacherByIdAsync(id, ct);
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<TeacherDto>>> CreateTeacher([FromBody] CreateTeacherRequest request, CancellationToken ct)
    {
        var result = await _teacherService.CreateTeacherAsync(request, ct);
        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<TeacherDto>>> UpdateTeacher(Guid id, [FromBody] UpdateTeacherRequest request, CancellationToken ct)
    {
        var result = await _teacherService.UpdateTeacherAsync(id, request, ct);
        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,SchoolAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeactivateTeacher(Guid id, CancellationToken ct)
    {
        var result = await _teacherService.DeactivateTeacherAsync(id, ct);
        return HandleResult(result);
    }
}
