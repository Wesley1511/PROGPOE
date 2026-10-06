using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_Participant_ReturnsCreated()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(Api.Email()));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.Equal("Participant", body!.Role);
    }

    [Fact]
    public async Task Register_Organiser_WithoutProfileDetails_ReturnsCreated()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", Api.OrganiserBody(Api.Email()));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("Organiser", (await res.Content.ReadFromJsonAsync<RegisterResponse>())!.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var email = Api.Email();
        await client.PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(email));

        var res = await client.PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(email));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(Api.Email(), password: "weak"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordsDoNotMatch_ReturnsBadRequest()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = Api.Email(),
            password = Api.Password,
            confirmPassword = "Different1!",
            firstName = "A",
            lastName = "B",
            role = "Organiser"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_ParticipantWithoutProfileDetails_ReturnsBadRequest()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = Api.Email(),
            password = Api.Password,
            confirmPassword = Api.Password,
            firstName = "A",
            lastName = "B",
            role = "Participant"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_ReturnsBadRequest()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            email = Api.Email(),
            password = Api.Password,
            confirmPassword = Api.Password,
            firstName = "A",
            lastName = "B",
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword()
    {
        var email = Api.Email();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(email));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        Assert.NotEqual(Api.Password, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Api.Password, user.PasswordHash));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndRole()
    {
        var client = factory.CreateClient();
        var email = Api.Email();
        await client.PostAsJsonAsync("/api/auth/register", Api.OrganiserBody(email));

        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Api.Password });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Organiser", body.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var email = Api.Email();
        await client.PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(email));

        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Wrong1234!" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = Api.Email(), password = Api.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_CreatesServerSideSession_UsableWithoutToken()
    {
        var client = factory.CreateClient();
        var email = Api.Email();
        await client.PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(email));
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = Api.Password });

        var res = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await res.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("Participant", me!.Role);
        Assert.Equal(email, me.Email);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = factory.CreateClient();
        var email = Api.Email();
        await client.PostAsJsonAsync("/api/auth/register", Api.ParticipantBody(email));
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = Api.Password });

        var logout = await client.PostAsync("/api/auth/logout", null);
        var afterLogout = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_ReturnsUnauthorized()
    {
        var res = await factory.CreateClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.real-token");

        var res = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Roles_IsPublic_AndListsBothRoles()
    {
        var res = await factory.CreateClient().GetAsync("/api/auth/roles");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var roles = await res.Content.ReadFromJsonAsync<List<RoleResponse>>();
        Assert.Contains(roles!, r => r.RoleName == "Organiser");
        Assert.Contains(roles!, r => r.RoleName == "Participant");
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsUnauthorized()
    {
        var (client, _) = await Api.SignInAsync(factory, "Participant");

        var res = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Wrong1234!", newPassword = "NewPassw0rd!", confirmNewPassword = "NewPassw0rd!" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_ReturnsNoContent()
    {
        var (client, _) = await Api.SignInAsync(factory, "Participant");

        var res = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = Api.Password, newPassword = "NewPassw0rd!", confirmNewPassword = "NewPassw0rd!" });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}