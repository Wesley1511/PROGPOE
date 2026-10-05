using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RaceDay.Api;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("RaceDay")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".RaceDay.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromHours(1);
});

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication("Smart")
    .AddPolicyScheme("Smart", "Session or Bearer", o =>
        o.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : SessionAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>(SessionAuthHandler.SchemeName, _ => { })
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwt["Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ClockSkew = TimeSpan.FromMinutes(1)
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<TokenService>();
builder.Services.AddHttpClient<IWeatherService, WeatherService>(c =>
{
    c.BaseAddress = new Uri("https://api.open-meteo.com/");
    c.Timeout = TimeSpan.FromSeconds(8);
});

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "Event management API for South African road running, walking and cycling events. " +
                      "Log in via POST /api/auth/login: the server creates a session (cookie) and also returns a JWT. " +
                      "Use either the session cookie (automatic in this UI) or click Authorize and paste the token."
    });
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT returned by POST /api/auth/login."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    if (db.Database.IsRelational() && db.Database.GetMigrations().Any()) db.Database.Migrate();
    else db.Database.EnsureCreated();
    if (app.Configuration.GetValue<bool>("SeedSampleData")) await DbSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1"));

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();

public partial class Program { }
