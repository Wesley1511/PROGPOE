using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using System.Reflection.Emit;

namespace RaceDay.Api.Data;

public class RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ParticipantProfile> ParticipantProfiles => Set<ParticipantProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<RouteWaypoint> RouteWaypoints => Set<RouteWaypoint>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<EventMedia> EventMedia => Set<EventMedia>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.ToTable("Role", t => t.HasCheckConstraint("CK_Role_RoleName", "[RoleName] IN (N'Organiser', N'Participant')"));
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleName).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(200);
            e.HasIndex(x => x.RoleName).IsUnique().HasDatabaseName("UQ_Role_RoleName");
            e.HasData(
                new Role { RoleId = 1, RoleName = RoleNames.Organiser, Description = "Creates and manages events, categories, and captures participant results." },
                new Role { RoleId = 2, RoleName = RoleNames.Participant, Description = "Browses events, enters a category, and tracks personal results." });
        });

        b.Entity<AppUser>(e =>
        {
            e.ToTable("AppUser");
            e.HasKey(x => x.UserId);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).HasMaxLength(256);
            e.Property(x => x.FirstName).HasMaxLength(60);
            e.Property(x => x.LastName).HasMaxLength(60);
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UQ_AppUser_Email");
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Profile).WithOne(p => p.User).HasForeignKey<ParticipantProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ParticipantProfile>(e =>
        {
            e.ToTable("ParticipantProfile", t =>
            {
                t.HasCheckConstraint("CK_ParticipantProfile_Gender", "[Gender] IN (N'Male', N'Female', N'Other')");
                t.HasCheckConstraint("CK_ParticipantProfile_Shirt", "[TShirtSize] IS NULL OR [TShirtSize] IN (N'XS',N'S',N'M',N'L',N'XL',N'XXL')");
            });
            e.HasKey(x => x.UserId);
            e.Property(x => x.Gender).HasMaxLength(10);
            e.Property(x => x.ClubName).HasMaxLength(100);
            e.Property(x => x.TShirtSize).HasMaxLength(5);
            e.Property(x => x.EmergencyContactName).HasMaxLength(100);
            e.Property(x => x.EmergencyContactPhone).HasMaxLength(20);
            e.Property(x => x.MedicalNotes).HasMaxLength(500);
        });

        b.Entity<Event>(e =>
        {
            e.ToTable("Event", t =>
            {
                t.HasCheckConstraint("CK_Event_EventType", "[EventType] IN (N'Run', N'Walk', N'Cycle', N'Trail', N'Triathlon')");
                t.HasCheckConstraint("CK_Event_Status", "[Status] IN (N'Draft', N'Published', N'Cancelled', N'Completed')");
                t.HasCheckConstraint("CK_Event_RegistrationDates", "[RegistrationClosesOn] >= [RegistrationOpensOn]");
                t.HasCheckConstraint("CK_Event_RegistrationBeforeRace", "[RegistrationClosesOn] <= [EventDate]");
            });
            e.HasKey(x => x.EventId);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.EventType).HasMaxLength(20);
            e.Property(x => x.VenueName).HasMaxLength(150);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.Province).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            e.HasIndex(x => x.EventDate).HasDatabaseName("IX_Event_EventDate");
            e.HasIndex(x => x.OrganiserId).HasDatabaseName("IX_Event_OrganiserId");
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents).HasForeignKey(x => x.OrganiserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EventCategory>(e =>
        {
            e.ToTable("EventCategory", t =>
            {
                t.HasCheckConstraint("CK_EventCategory_Distance", "[DistanceKm] > 0");
                t.HasCheckConstraint("CK_EventCategory_EntryFee", "[EntryFee] >= 0");
                t.HasCheckConstraint("CK_EventCategory_MaxParts", "[MaxParticipants] IS NULL OR [MaxParticipants] > 0");
                t.HasCheckConstraint("CK_EventCategory_MinimumAge", "[MinimumAge] BETWEEN 0 AND 100");
            });
            e.HasKey(x => x.CategoryId);
            e.HasAlternateKey(x => new { x.EventId, x.CategoryId }).HasName("UQ_EventCategory_EventCat");
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.DistanceKm).HasPrecision(6, 2);
            e.Property(x => x.EntryFee).HasPrecision(10, 2);
            e.Property(x => x.StartTime).HasColumnType("time(0)");
            e.HasIndex(x => new { x.EventId, x.Name }).IsUnique().HasDatabaseName("UQ_EventCategory_EventName");
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RouteWaypoint>(e =>
        {
            e.ToTable("RouteWaypoint", t =>
            {
                t.HasCheckConstraint("CK_RouteWaypoint_Type", "[WaypointType] IN (N'Start', N'WaterPoint', N'Marker', N'MedicalPoint', N'CutOff', N'Finish')");
                t.HasCheckConstraint("CK_RouteWaypoint_Sequence", "[SequenceNumber] > 0");
                t.HasCheckConstraint("CK_RouteWaypoint_Lat", "[Latitude] BETWEEN -90 AND 90");
                t.HasCheckConstraint("CK_RouteWaypoint_Lng", "[Longitude] BETWEEN -180 AND 180");
            });
            e.HasKey(x => x.WaypointId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.WaypointType).HasMaxLength(20);
            e.Property(x => x.DistanceMarkerKm).HasPrecision(6, 2);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.HasIndex(x => new { x.CategoryId, x.SequenceNumber }).IsUnique().HasDatabaseName("UQ_RouteWaypoint_Sequence");
            e.HasOne(x => x.Category).WithMany(c => c.Waypoints).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolment", t =>
            {
                t.HasCheckConstraint("CK_Enrolment_Status", "[Status] IN (N'Pending', N'Confirmed', N'Withdrawn', N'Cancelled')");
                t.HasCheckConstraint("CK_Enrolment_AmountPaid", "[AmountPaid] >= 0");
            });
            e.HasKey(x => x.EnrolmentId);
            e.Property(x => x.RaceNumber).HasMaxLength(10);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.AmountPaid).HasPrecision(10, 2);
            e.Property(x => x.PaymentReference).HasMaxLength(50);
            e.Property(x => x.EnrolledOn).HasColumnType("datetime2(0)");
            e.HasIndex(x => new { x.EventId, x.ParticipantId }).IsUnique().HasDatabaseName("UQ_Enrolment_EventParticipant");
            e.HasIndex(x => new { x.EventId, x.RaceNumber }).IsUnique().HasDatabaseName("UQ_Enrolment_EventRaceNumber");
            e.HasIndex(x => x.ParticipantId).HasDatabaseName("IX_Enrolment_ParticipantId");
            e.HasIndex(x => x.CategoryId).HasDatabaseName("IX_Enrolment_CategoryId");
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => new { x.EventId, x.CategoryId })
                .HasPrincipalKey(c => new { c.EventId, c.CategoryId })
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Participant).WithMany(u => u.Enrolments).HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Result>(e =>
        {
            e.ToTable("Result", t =>
            {
                t.HasCheckConstraint("CK_Result_Status", "[Status] IN (N'Finished', N'DNF', N'DNS', N'DQ')");
                t.HasCheckConstraint("CK_Result_FinishTime", "[FinishTimeSeconds] IS NULL OR [FinishTimeSeconds] > 0");
            });
            e.HasKey(x => x.ResultId);
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.CapturedAt).HasColumnType("datetime2(0)");
            e.HasIndex(x => x.EnrolmentId).IsUnique().HasDatabaseName("UQ_Result_Enrolment");
            e.HasIndex(x => x.CapturedByUserId).HasDatabaseName("IX_Result_CapturedByUserId");
            e.HasOne(x => x.Enrolment).WithOne(en => en.Result).HasForeignKey<Result>(x => x.EnrolmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CapturedBy).WithMany().HasForeignKey(x => x.CapturedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EventMedia>(e =>
        {
            e.ToTable("EventMedia", t =>
            {
                t.HasCheckConstraint("CK_EventMedia_MediaType", "[MediaType] IN (N'Banner', N'RouteMap', N'Gallery', N'Document')");
                t.HasCheckConstraint("CK_EventMedia_Size", "[SizeInBytes] IS NULL OR [SizeInBytes] > 0");
            });
            e.HasKey(x => x.MediaId);
            e.Property(x => x.MediaType).HasMaxLength(20);
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.BlobUri).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.UploadedAt).HasColumnType("datetime2(0)");
            e.HasIndex(x => x.BlobUri).IsUnique().HasDatabaseName("UQ_EventMedia_BlobUri");
            e.HasIndex(x => x.EventId).HasDatabaseName("IX_EventMedia_EventId");
            e.HasOne(x => x.Event).WithMany(ev => ev.Media).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
