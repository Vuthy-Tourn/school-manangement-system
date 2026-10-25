using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexora.Application.Common;
using Nexora.Application.DTOs;
using Nexora.Domain.Enums;
using Nexora.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Nexora.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType == typeof(IApplicationDbContext)).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Ensure DB is created and seeded
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            db.Database.EnsureCreated();
            seeder.SeedAsync().GetAwaiter().GetResult();
        });
    }
}

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturn_Ok()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_ShouldReturn_AccessTokenAndProfile()
    {
        var loginRequest = new LoginRequest("admin", "Admin@123!");
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        content.Should().NotBeNull();
        content!.Success.Should().BeTrue();
        content.Data.Should().NotBeNull();
        content.Data!.AccessToken.Should().NotBeNullOrEmpty();
        content.Data.User.Role.Should().Be(UserRole.SchoolAdmin);
        content.Data.User.SchoolName.Should().Be("Angkor International Academy");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturn_BadRequest()
    {
        var loginRequest = new LoginRequest("admin", "WrongPassword!");
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var content = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        content.Should().NotBeNull();
        content!.Success.Should().BeFalse();
        content.Message.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task GetStudents_WithoutToken_ShouldReturn_Unauthorized()
    {
        var response = await _client.GetAsync("/api/students");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStudents_WithAdminToken_ShouldReturn_SeededStudents()
    {
        // 1. Authenticate as Admin
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin@123!"));
        var authData = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        var token = authData!.Data!.AccessToken;

        // 2. Request students
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/students");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaginatedResult<StudentDto>>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.Items.Should().NotBeEmpty();
        result.Data.Items.Should().Contain(s => s.AdmissionNumber == "ADM-2026-0001");
    }

    [Fact]
    public async Task StudentUser_CannotCreateStudent()
    {
        // 1. Authenticate as Student
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("student.visal", "Student@123!"));
        var authData = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        var token = authData!.Data!.AccessToken;

        // 2. Try to invoke admin-only endpoint
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/students");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new CreateStudentRequest(
            "ADM-9999", "Test", "Student", DateTime.UtcNow.AddYears(-15), "Male", "O+", DateTime.UtcNow,
            null, null, null, null, null, null, null, null, null, null
        ));

        var response = await _client.SendAsync(request);
        // Should be Forbidden (403)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminDashboard_WithAdminToken_ShouldReturn_Stats()
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin@123!"));
        var authData = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOptions);
        var token = authData!.Data!.AccessToken;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboards/admin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminDashboardStatsDto>>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data!.TotalStudents.Should().BeGreaterThan(0);
        result.Data.TotalTeachers.Should().BeGreaterThan(0);
    }
}
