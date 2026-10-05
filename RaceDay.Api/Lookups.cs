namespace RaceDay.Api;

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public static readonly string[] All = [Organiser, Participant];
}

public static class Lookups
{
    public static readonly string[] EventTypes = ["Run", "Walk", "Cycle", "Trail", "Triathlon"];
    public static readonly string[] EventStatuses = ["Draft", "Published", "Cancelled", "Completed"];
    public static readonly string[] Provinces =
    [
        "Gauteng", "Western Cape", "KwaZulu-Natal", "Eastern Cape", "Free State", "Limpopo", "Mpumalanga", "North West", "Northern Cape"
    ];
    public static readonly string[] Genders = ["Male", "Female", "Other"];
    public static readonly string[] ShirtSizes = ["XS", "S", "M", "L", "XL", "XXL"];
    public static readonly string[] WaypointTypes = ["Start", "WaterPoint", "Marker", "MedicalPoint", "CutOff", "Finish"];
    public static readonly string[] ResultStatuses = ["Finished", "DNF", "DNS", "DQ"];
    public static readonly string[] ActiveEnrolment = ["Pending", "Confirmed"];
    public static readonly string[] OrganiserSettableEnrolment = ["Pending", "Confirmed", "Cancelled"];
}

public static class Clock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2));
}

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string Name = "Name";
}

public static class Passwords
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 11);

    public static bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }

    public static string? Check(string password)
    {
        if (password.Length < 8) return "Password must be at least 8 characters.";
        if (!password.Any(char.IsUpper)) return "Password must contain an uppercase letter.";
        if (!password.Any(char.IsLower)) return "Password must contain a lowercase letter.";
        if (!password.Any(char.IsDigit)) return "Password must contain a digit.";
        return null;
    }
}

public static class ProfileValidator
{
    public static string? Validate(DateOnly? dob, string? gender, string? shirt, string? contactName, string? contactPhone)
    {
        if (dob is null) return "Date of birth is required.";
        if (dob.Value >= Clock.Today) return "Date of birth must be in the past.";
        if (gender is null || !Lookups.Genders.Contains(gender)) return $"Gender must be one of: {string.Join(", ", Lookups.Genders)}.";
        if (shirt is not null && !Lookups.ShirtSizes.Contains(shirt)) return $"T-shirt size must be one of: {string.Join(", ", Lookups.ShirtSizes)}.";
        if (string.IsNullOrWhiteSpace(contactName) || string.IsNullOrWhiteSpace(contactPhone)) return "Emergency contact name and phone are required.";
        return null;
    }
}

