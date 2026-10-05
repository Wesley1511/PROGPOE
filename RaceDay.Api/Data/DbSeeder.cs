using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

public static class DbSeeder
{
    public const string SamplePassword = "Password123!";

    public static async Task SeedAsync(RaceDayDbContext db)
    {
        var hash = Passwords.Hash(SamplePassword);

        var placeholders = await db.Users.Where(u => u.PasswordHash.StartsWith("$2a$11$SeedHash")).ToListAsync();
        foreach (var u in placeholders) u.PasswordHash = hash;
        if (placeholders.Count > 0) await db.SaveChangesAsync();
        if (await db.Users.AnyAsync()) return;

        AppUser NewUser(int roleId, string email, string first, string last, string phone) =>
            new() { RoleId = roleId, Email = email, FirstName = first, LastName = last, PhoneNumber = phone, PasswordHash = hash };

        var thabo = NewUser(1, "thabo.molefe@comradesrace.co.za", "Thabo", "Molefe", "0821234567");
        var lerato = NewUser(1, "lerato.dlamini@ctcycletour.co.za", "Lerato", "Dlamini", "0837654321");
        var sipho = NewUser(2, "sipho.khumalo@gmail.com", "Sipho", "Khumalo", "0713334444");
        var anele = NewUser(2, "anele.naidoo@outlook.com", "Anele", "Naidoo", "0725556666");
        var johan = NewUser(2, "johan.vanwyk@webmail.co.za", "Johan", "van Wyk", "0617778888");
        var nomsa = NewUser(2, "nomsa.mahlangu@gmail.com", "Nomsa", "Mahlangu", "0829990000");
        AppUser[] users = [thabo, lerato, sipho, anele, johan, nomsa];

        ParticipantProfile Profile(AppUser u, DateOnly dob, string gender, string club, string shirt, string contact, string phone, string? notes = null) =>
            new() { User = u, DateOfBirth = dob, Gender = gender, ClubName = club, TShirtSize = shirt, EmergencyContactName = contact, EmergencyContactPhone = phone, MedicalNotes = notes };

        ParticipantProfile[] profiles =
        [
            Profile(sipho, new(1994, 3, 12), "Male", "Soweto Striders AC", "M", "Zanele Khumalo", "0715551212"),
            Profile(anele, new(1989, 11, 2), "Female", "Durban Athletic Club", "S", "Ravi Naidoo", "0824448899", "Mild asthma - carries inhaler."),
            Profile(johan, new(1977, 6, 25), "Male", "Pretoria Wheelers CC", "XL", "Marie van Wyk", "0833219876"),
            Profile(nomsa, new(2001, 1, 30), "Female", "Tshwane Runners", "M", "Peter Mahlangu", "0761239876")
        ];

        Event Ev(AppUser org, string name, string desc, string type, DateOnly date, string venue, string city, string prov, decimal lat, decimal lng, DateOnly opens, DateOnly closes) =>
            new() { Organiser = org, Name = name, Description = desc, EventType = type, EventDate = date, VenueName = venue, City = city, Province = prov, Latitude = lat, Longitude = lng, RegistrationOpensOn = opens, RegistrationClosesOn = closes, Status = "Published" };

        var soweto = Ev(thabo, "Soweto Marathon 2026",
            "South Africa's toughest marathon through the historic streets of Soweto, taking in Orlando Stadium, the Hector Pieterson Memorial and Vilakazi Street.",
            "Run", new(2026, 11, 1), "Nasrec Expo Centre", "Johannesburg", "Gauteng", -26.240000m, 27.983000m, new(2026, 6, 1), new(2026, 10, 20));
        var ctct = Ev(lerato, "Cape Town Cycle Tour 2026",
            "The world's largest timed cycle race, a 109 km loop around the Cape Peninsula including Chapman's Peak and Suikerbossie.",
            "Cycle", new(2026, 3, 8), "Grand Parade", "Cape Town", "Western Cape", -33.925800m, 18.423200m, new(2025, 10, 1), new(2026, 2, 20));
        var walk = Ev(thabo, "Durban Beachfront Charity Walk 2026",
            "A family-friendly walk along the Golden Mile promenade raising funds for local school sports programmes.",
            "Walk", new(2026, 9, 26), "uShaka Marine World", "Durban", "KwaZulu-Natal", -29.867000m, 31.044000m, new(2026, 5, 1), new(2026, 9, 20));
        Event[] events = [soweto, ctct, walk];

        EventCategory Cat(Event e, string name, decimal km, decimal fee, TimeOnly start, int? max, int minAge, int? cutOff) =>
            new() { Event = e, Name = name, DistanceKm = km, EntryFee = fee, StartTime = start, MaxParticipants = max, MinimumAge = minAge, CutOffMinutes = cutOff };

        var swFull = Cat(soweto, "Full Marathon 42.2 km", 42.2m, 450m, new(6, 0), 12000, 20, 360);
        var swHalf = Cat(soweto, "Half Marathon 21.1 km", 21.1m, 300m, new(6, 30), 8000, 16, 210);
        var swFun = Cat(soweto, "Fun Run 10 km", 10m, 150m, new(7, 0), 5000, 0, 120);
        var ctRace = Cat(ctct, "Race Route 109 km", 109m, 850m, new(6, 15), 30000, 18, 480);
        var ctLite = Cat(ctct, "Lite Route 42 km", 42m, 480m, new(8, 0), 4000, 12, 240);
        var wk5 = Cat(walk, "Charity Walk 5 km", 5m, 80m, new(8, 0), 2500, 0, null);
        var wk10 = Cat(walk, "Charity Walk 10 km", 10m, 120m, new(7, 30), 1500, 12, 150);
        EventCategory[] categories = [swFull, swHalf, swFun, ctRace, ctLite, wk5, wk10];

        RouteWaypoint Wp(EventCategory c, int seq, string name, string type, decimal km, decimal lat, decimal lng) =>
            new() { Category = c, SequenceNumber = seq, Name = name, WaypointType = type, DistanceMarkerKm = km, Latitude = lat, Longitude = lng };

        RouteWaypoint[] waypoints =
        [
            Wp(swFull, 1, "Nasrec Start Chute", "Start", 0m, -26.240000m, 27.983000m),
            Wp(swFull, 2, "Orlando Stadium", "WaterPoint", 12.5m, -26.267000m, 27.929000m),
            Wp(swFull, 3, "Vilakazi Street", "Marker", 21.1m, -26.238000m, 27.907000m),
            Wp(swFull, 4, "Hector Pieterson Memorial", "MedicalPoint", 30m, -26.233000m, 27.909000m),
            Wp(swFull, 5, "Nasrec Finish", "Finish", 42.2m, -26.240000m, 27.983000m),
            Wp(wk5, 1, "uShaka Start", "Start", 0m, -29.867000m, 31.044000m),
            Wp(wk5, 2, "Suncoast Turnaround", "Marker", 2.5m, -29.843000m, 31.041000m),
            Wp(wk5, 3, "uShaka Finish", "Finish", 5m, -29.867000m, 31.044000m)
        ];

        Enrolment En(EventCategory c, AppUser p, string race, string status, decimal paid, string? pay) =>
            new() { Category = c, Participant = p, RaceNumber = race, Status = status, AmountPaid = paid, PaymentReference = pay };

        var e1 = En(swFull, sipho, "A1042", "Confirmed", 450m, "PAY-2026-000101");
        var e2 = En(swHalf, nomsa, "B2087", "Confirmed", 300m, "PAY-2026-000102");
        var e3 = En(swFun, anele, "C3311", "Confirmed", 150m, "PAY-2026-000103");
        var e4 = En(ctRace, johan, "D0455", "Confirmed", 850m, "PAY-2026-000104");
        var e5 = En(ctLite, anele, "E1120", "Confirmed", 480m, "PAY-2026-000105");
        var e6 = En(wk5, nomsa, "F0012", "Confirmed", 80m, "PAY-2026-000106");
        var e7 = En(wk10, sipho, "G0301", "Pending", 0m, null);
        Enrolment[] enrolments = [e1, e2, e3, e4, e5, e6, e7];

        Result Res(Enrolment e, int? time, int? overall, int? cat, string status) =>
            new() { Enrolment = e, CapturedBy = thabo, FinishTimeSeconds = time, OverallPosition = overall, CategoryPosition = cat, Status = status };

        Result[] results =
        [
            Res(e1, 13860, 42, 42, "Finished"),
            Res(e2, 7320, 15, 4, "Finished"),
            Res(e3, 3540, 88, 88, "Finished"),
            Res(e4, 15300, 610, 610, "Finished"),
            Res(e5, null, null, null, "DNF")
        ];

        EventMedia Media(Event e, string type, string file, string contentType, long size) =>
            new()
            {
                Event = e,
                UploadedBy = e.Organiser,
                MediaType = type,
                FileName = file,
                ContentType = contentType,
                SizeInBytes = size,
                BlobUri = $"https://racedaystorage.blob.core.windows.net/event-media/{file}"
            };

        EventMedia[] media =
        [
            Media(soweto, "Banner", "soweto-2026-banner.jpg", "image/jpeg", 482311),
            Media(soweto, "RouteMap", "soweto-2026-route.png", "image/png", 921044),
            Media(ctct, "Banner", "ctct-2026-banner.jpg", "image/jpeg", 517820),
            Media(walk, "Document", "durban-walk-indemnity.pdf", "application/pdf", 145602)
        ];

        db.Users.AddRange(users);
        db.ParticipantProfiles.AddRange(profiles);
        db.Events.AddRange(events);
        db.EventCategories.AddRange(categories);
        db.RouteWaypoints.AddRange(waypoints);
        db.Enrolments.AddRange(enrolments);
        db.Results.AddRange(results);
        db.EventMedia.AddRange(media);
        await db.SaveChangesAsync();
    }
}
