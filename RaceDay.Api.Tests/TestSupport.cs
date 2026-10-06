using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["SeedSampleData"] = "false" }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}

public static class Api
{
    public const string Password = "Passw0rd!";

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static string Email() => $"{Guid.NewGuid():N}@raceday.test";

    public static object ParticipantBody(string email, string dob = "1990-05-05", string password = Password) => new
    {
        email,
        password,
        confirmPassword = password,
        firstName = "Test",
        lastName = "Runner",
        phoneNumber = "0821234567",
        role = "Participant",
        dateOfBirth = dob,
        gender = "Male",
        clubName = "Test AC",
        tShirtSize = "M",
        emergencyContactName = "Contact Person",
        emergencyContactPhone = "0829999999"
    };

    public static object OrganiserBody(string email) => new
    {
        email,
        password = Password,
        confirmPassword = Password,
        firstName = "Test",
        lastName = "Organiser",
        phoneNumber = "0831234567",
        role = "Organiser"
    };

    public static object EventBody(string? name = null, int daysAhead = 60, int closesInDays = 50,
        string province = "Gauteng", string? status = "Published") => new
        {
            name = name ?? $"Test Run {Guid.NewGuid().ToString("N")[..8]}",
            description = "A test event.",
            eventType = "Run",
            eventDate = Today.AddDays(daysAhead),
            venueName = "Test Park",
            city = "Pretoria",
            province,
            latitude = -25.7479,
            longitude = 28.2293,
            registrationOpensOn = Today.AddDays(-30),
            registrationClosesOn = Today.AddDays(closesInDays),
            status
        };

    public static async Task<(HttpClient Client, int UserId)> SignInAsync(ApiFactory factory, string role, string dob = "1990-05-05")
    {
        var email = Email();
        var client = factory.CreateClient();
        var body = role == "Organiser" ? OrganiserBody(email) : ParticipantBody(email, dob);
        (await client.PostAsJsonAsync("/api/auth/register", body)).EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var data = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.Token);
        return (client, data.UserId);
    }

    public static async Task<EventDetailResponse> CreateEventAsync(HttpClient organiser, object? body = null)
    {
        var res = await organiser.PostAsJsonAsync("/api/events", body ?? EventBody());
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<EventDetailResponse>())!;
    }

    public static async Task<CategoryResponse> CreateCategoryAsync(HttpClient organiser, int eventId,
        decimal fee = 100m, int? max = null, int minimumAge = 0, string? name = null)
    {
        var res = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = name ?? $"10 km {Guid.NewGuid().ToString("N")[..6]}",
            distanceKm = 10m,
            entryFee = fee,
            startTime = "07:00:00",
            maxParticipants = max,
            minimumAge,
            cutOffMinutes = 120
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CategoryResponse>())!;
    }

    public static Task<HttpResponseMessage> EnrolAsync(HttpClient participant, int eventId, int categoryId, decimal paid = 100m) =>
        participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments",
            new { categoryId, amountPaid = paid, paymentReference = $"PAY-{Guid.NewGuid().ToString("N")[..8]}" });

    public static async Task<EnrolmentResponse> EnrolOkAsync(HttpClient participant, int eventId, int categoryId, decimal paid = 100m)
    {
        var res = await EnrolAsync(participant, eventId, categoryId, paid);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<EnrolmentResponse>())!;
    }
}

public static class HttpClientPatchExtensions
{
    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsync(url, JsonContent.Create(body));
}
