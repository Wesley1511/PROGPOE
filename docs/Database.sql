USE master;
GO

IF DB_ID('RaceDayDb') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDb;
END
GO

CREATE DATABASE RaceDayDb;
GO

USE RaceDayDb;
GO

CREATE TABLE dbo.Role
(
    RoleId      INT             IDENTITY(1,1)   NOT NULL,
    RoleName    NVARCHAR(20)                    NOT NULL,
    Description NVARCHAR(200)                       NULL,

    CONSTRAINT PK_Role          PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Role_RoleName UNIQUE (RoleName),
    CONSTRAINT CK_Role_RoleName CHECK (RoleName IN (N'Organiser', N'Participant'))
);
GO

CREATE TABLE dbo.AppUser
(
    UserId       INT            IDENTITY(1,1)   NOT NULL,
    RoleId       INT                            NOT NULL,
    Email        NVARCHAR(256)                  NOT NULL,
    PasswordHash NVARCHAR(256)                  NOT NULL,
    FirstName    NVARCHAR(60)                   NOT NULL,
    LastName     NVARCHAR(60)                   NOT NULL,
    PhoneNumber  NVARCHAR(20)                       NULL,
    IsActive     BIT            NOT NULL        CONSTRAINT DF_AppUser_IsActive   DEFAULT (1),
    CreatedAt    DATETIME2(0)   NOT NULL        CONSTRAINT DF_AppUser_CreatedAt  DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_AppUser        PRIMARY KEY (UserId),
    CONSTRAINT UQ_AppUser_Email  UNIQUE (Email),
    CONSTRAINT FK_AppUser_Role   FOREIGN KEY (RoleId) REFERENCES dbo.Role (RoleId),
    CONSTRAINT CK_AppUser_Email  CHECK (Email LIKE N'%_@_%._%')
);
GO

CREATE TABLE dbo.ParticipantProfile
(
    UserId                INT             NOT NULL,
    DateOfBirth           DATE            NOT NULL,
    Gender                NVARCHAR(10)    NOT NULL,
    ClubName              NVARCHAR(100)       NULL,
    TShirtSize            NVARCHAR(5)         NULL,
    EmergencyContactName  NVARCHAR(100)   NOT NULL,
    EmergencyContactPhone NVARCHAR(20)    NOT NULL,
    MedicalNotes          NVARCHAR(500)       NULL,

    CONSTRAINT PK_ParticipantProfile          PRIMARY KEY (UserId),
    CONSTRAINT FK_ParticipantProfile_AppUser  FOREIGN KEY (UserId)
        REFERENCES dbo.AppUser (UserId) ON DELETE CASCADE,
    CONSTRAINT CK_ParticipantProfile_Gender   CHECK (Gender IN (N'Male', N'Female', N'Other')),
    CONSTRAINT CK_ParticipantProfile_Shirt    CHECK (TShirtSize IS NULL OR TShirtSize IN (N'XS',N'S',N'M',N'L',N'XL',N'XXL')),
    CONSTRAINT CK_ParticipantProfile_DOB      CHECK (DateOfBirth < CAST(GETDATE() AS DATE))
);
GO

CREATE TABLE dbo.Event
(
    EventId              INT            IDENTITY(1,1) NOT NULL,
    OrganiserId          INT                          NOT NULL,
    Name                 NVARCHAR(150)                NOT NULL,
    Description          NVARCHAR(1000)                   NULL,
    EventType            NVARCHAR(20)                 NOT NULL,
    EventDate            DATE                         NOT NULL,
    VenueName            NVARCHAR(150)                NOT NULL,
    City                 NVARCHAR(80)                 NOT NULL,
    Province             NVARCHAR(50)                 NOT NULL,
    Latitude             DECIMAL(9,6)                     NULL,
    Longitude            DECIMAL(9,6)                     NULL,
    RegistrationOpensOn  DATE                         NOT NULL,
    RegistrationClosesOn DATE                         NOT NULL,
    Status               NVARCHAR(20)   NOT NULL      CONSTRAINT DF_Event_Status    DEFAULT (N'Published'),
    CreatedAt            DATETIME2(0)   NOT NULL      CONSTRAINT DF_Event_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Event                  PRIMARY KEY (EventId),
    CONSTRAINT FK_Event_Organiser        FOREIGN KEY (OrganiserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT CK_Event_EventType        CHECK (EventType IN (N'Run', N'Walk', N'Cycle', N'Trail', N'Triathlon')),
    CONSTRAINT CK_Event_Status           CHECK (Status IN (N'Draft', N'Published', N'Cancelled', N'Completed')),
    CONSTRAINT CK_Event_RegistrationDates CHECK (RegistrationClosesOn >= RegistrationOpensOn),
    CONSTRAINT CK_Event_RegistrationBeforeRace CHECK (RegistrationClosesOn <= EventDate),
    CONSTRAINT CK_Event_Province         CHECK (Province IN (N'Gauteng', N'Western Cape', N'KwaZulu-Natal',
                                                            N'Eastern Cape', N'Free State', N'Limpopo',
                                                            N'Mpumalanga', N'North West', N'Northern Cape'))
);
GO

CREATE TABLE dbo.EventCategory
(
    CategoryId      INT            IDENTITY(1,1) NOT NULL,
    EventId         INT                          NOT NULL,
    Name            NVARCHAR(80)                 NOT NULL,
    DistanceKm      DECIMAL(6,2)                 NOT NULL,
    EntryFee        DECIMAL(10,2)  NOT NULL      CONSTRAINT DF_EventCategory_EntryFee DEFAULT (0),
    StartTime       TIME(0)                      NOT NULL,
    MaxParticipants INT                              NULL,
    MinimumAge      INT            NOT NULL      CONSTRAINT DF_EventCategory_MinimumAge DEFAULT (0),
    CutOffMinutes   INT                              NULL,

    CONSTRAINT PK_EventCategory              PRIMARY KEY (CategoryId),
    CONSTRAINT UQ_EventCategory_EventCat     UNIQUE (EventId, CategoryId),
    CONSTRAINT UQ_EventCategory_EventName    UNIQUE (EventId, Name),
    CONSTRAINT FK_EventCategory_Event        FOREIGN KEY (EventId)
        REFERENCES dbo.Event (EventId) ON DELETE CASCADE,
    CONSTRAINT CK_EventCategory_Distance     CHECK (DistanceKm > 0),
    CONSTRAINT CK_EventCategory_EntryFee     CHECK (EntryFee >= 0),
    CONSTRAINT CK_EventCategory_MaxParts     CHECK (MaxParticipants IS NULL OR MaxParticipants > 0),
    CONSTRAINT CK_EventCategory_MinimumAge   CHECK (MinimumAge BETWEEN 0 AND 100)
);
GO

CREATE TABLE dbo.RouteWaypoint
(
    WaypointId       INT            IDENTITY(1,1) NOT NULL,
    CategoryId       INT                          NOT NULL,
    SequenceNumber   INT                          NOT NULL,
    Name             NVARCHAR(100)                NOT NULL,
    WaypointType     NVARCHAR(20)   NOT NULL      CONSTRAINT DF_RouteWaypoint_Type DEFAULT (N'Marker'),
    DistanceMarkerKm DECIMAL(6,2)                     NULL,
    Latitude         DECIMAL(9,6)                 NOT NULL,
    Longitude        DECIMAL(9,6)                 NOT NULL,

    CONSTRAINT PK_RouteWaypoint             PRIMARY KEY (WaypointId),
    CONSTRAINT UQ_RouteWaypoint_Sequence    UNIQUE (CategoryId, SequenceNumber),
    CONSTRAINT FK_RouteWaypoint_Category    FOREIGN KEY (CategoryId)
        REFERENCES dbo.EventCategory (CategoryId) ON DELETE CASCADE,
    CONSTRAINT CK_RouteWaypoint_Type        CHECK (WaypointType IN (N'Start', N'WaterPoint', N'Marker',
                                                                   N'MedicalPoint', N'CutOff', N'Finish')),
    CONSTRAINT CK_RouteWaypoint_Sequence    CHECK (SequenceNumber > 0),
    CONSTRAINT CK_RouteWaypoint_Lat         CHECK (Latitude BETWEEN -90 AND 90),
    CONSTRAINT CK_RouteWaypoint_Lng         CHECK (Longitude BETWEEN -180 AND 180)
);
GO

CREATE TABLE dbo.Enrolment
(
    EnrolmentId      INT            IDENTITY(1,1) NOT NULL,
    EventId          INT                          NOT NULL,
    CategoryId       INT                          NOT NULL,
    ParticipantId    INT                          NOT NULL,
    RaceNumber       NVARCHAR(10)                     NULL,
    EnrolledOn       DATETIME2(0)   NOT NULL      CONSTRAINT DF_Enrolment_EnrolledOn DEFAULT (SYSUTCDATETIME()),
    Status           NVARCHAR(20)   NOT NULL      CONSTRAINT DF_Enrolment_Status     DEFAULT (N'Confirmed'),
    AmountPaid       DECIMAL(10,2)  NOT NULL      CONSTRAINT DF_Enrolment_AmountPaid DEFAULT (0),
    PaymentReference NVARCHAR(50)                     NULL,

    CONSTRAINT PK_Enrolment                     PRIMARY KEY (EnrolmentId),
    CONSTRAINT UQ_Enrolment_EventParticipant    UNIQUE (EventId, ParticipantId),
    CONSTRAINT UQ_Enrolment_EventRaceNumber     UNIQUE (EventId, RaceNumber),
    CONSTRAINT FK_Enrolment_Participant         FOREIGN KEY (ParticipantId)
        REFERENCES dbo.AppUser (UserId),
    CONSTRAINT FK_Enrolment_EventCategory       FOREIGN KEY (EventId, CategoryId)
        REFERENCES dbo.EventCategory (EventId, CategoryId) ON DELETE CASCADE,
    CONSTRAINT CK_Enrolment_Status              CHECK (Status IN (N'Pending', N'Confirmed', N'Withdrawn', N'Cancelled')),
    CONSTRAINT CK_Enrolment_AmountPaid          CHECK (AmountPaid >= 0)
);
GO

CREATE TABLE dbo.Result
(
    ResultId          INT            IDENTITY(1,1) NOT NULL,
    EnrolmentId       INT                          NOT NULL,
    CapturedByUserId  INT                          NOT NULL,
    FinishTimeSeconds INT                              NULL,
    OverallPosition   INT                              NULL,
    CategoryPosition  INT                              NULL,
    Status            NVARCHAR(10)   NOT NULL      CONSTRAINT DF_Result_Status     DEFAULT (N'Finished'),
    CapturedAt        DATETIME2(0)   NOT NULL      CONSTRAINT DF_Result_CapturedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Result                 PRIMARY KEY (ResultId),
    CONSTRAINT UQ_Result_Enrolment       UNIQUE (EnrolmentId),
    CONSTRAINT FK_Result_Enrolment       FOREIGN KEY (EnrolmentId)
        REFERENCES dbo.Enrolment (EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT FK_Result_CapturedBy      FOREIGN KEY (CapturedByUserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT CK_Result_Status          CHECK (Status IN (N'Finished', N'DNF', N'DNS', N'DQ')),
    CONSTRAINT CK_Result_FinishTime      CHECK (FinishTimeSeconds IS NULL OR FinishTimeSeconds > 0),
    CONSTRAINT CK_Result_Positions       CHECK ((OverallPosition  IS NULL OR OverallPosition  > 0)
                                            AND (CategoryPosition IS NULL OR CategoryPosition > 0)),

    CONSTRAINT CK_Result_FinishedHasTime CHECK ((Status = N'Finished' AND FinishTimeSeconds IS NOT NULL)
                                             OR (Status <> N'Finished' AND OverallPosition IS NULL))
);
GO

CREATE TABLE dbo.EventMedia
(
    MediaId          INT            IDENTITY(1,1) NOT NULL,
    EventId          INT                          NOT NULL,
    UploadedByUserId INT                          NOT NULL,
    MediaType        NVARCHAR(20)   NOT NULL      CONSTRAINT DF_EventMedia_MediaType DEFAULT (N'Gallery'),
    FileName         NVARCHAR(200)                NOT NULL,
    BlobUri          NVARCHAR(500)                NOT NULL,
    ContentType      NVARCHAR(100)                NOT NULL,
    SizeInBytes      BIGINT                           NULL,
    UploadedAt       DATETIME2(0)   NOT NULL      CONSTRAINT DF_EventMedia_UploadedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_EventMedia             PRIMARY KEY (MediaId),
    CONSTRAINT UQ_EventMedia_BlobUri     UNIQUE (BlobUri),
    CONSTRAINT FK_EventMedia_Event       FOREIGN KEY (EventId)
        REFERENCES dbo.Event (EventId) ON DELETE CASCADE,
    CONSTRAINT FK_EventMedia_UploadedBy  FOREIGN KEY (UploadedByUserId) REFERENCES dbo.AppUser (UserId),
    CONSTRAINT CK_EventMedia_MediaType   CHECK (MediaType IN (N'Banner', N'RouteMap', N'Gallery', N'Document')),
    CONSTRAINT CK_EventMedia_Size        CHECK (SizeInBytes IS NULL OR SizeInBytes > 0)
);
GO

CREATE INDEX IX_AppUser_RoleId            ON dbo.AppUser (RoleId);
CREATE INDEX IX_Event_EventDate           ON dbo.Event (EventDate) INCLUDE (Name, City, Province, Status);
CREATE INDEX IX_Event_OrganiserId         ON dbo.Event (OrganiserId);
CREATE INDEX IX_EventCategory_EventId     ON dbo.EventCategory (EventId);
CREATE INDEX IX_Enrolment_ParticipantId   ON dbo.Enrolment (ParticipantId) INCLUDE (Status, EnrolledOn);
CREATE INDEX IX_Enrolment_CategoryId      ON dbo.Enrolment (CategoryId);
CREATE INDEX IX_Result_CapturedByUserId   ON dbo.Result (CapturedByUserId);
CREATE INDEX IX_EventMedia_EventId        ON dbo.EventMedia (EventId);
GO

INSERT INTO dbo.Role (RoleName, Description) VALUES
    (N'Organiser',   N'Creates and manages events, categories, and captures participant results.'),
    (N'Participant', N'Browses events, enters a category, and tracks personal results.');
GO

DECLARE @OrganiserRoleId   INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = N'Organiser');
DECLARE @ParticipantRoleId INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = N'Participant');

INSERT INTO dbo.AppUser (RoleId, Email, PasswordHash, FirstName, LastName, PhoneNumber) VALUES
    (@OrganiserRoleId,   N'thabo.molefe@comradesrace.co.za', N'$2a$11$SeedHashOrganiser000000001', N'Thabo',   N'Molefe',    N'0821234567'),
    (@OrganiserRoleId,   N'lerato.dlamini@ctcycletour.co.za', N'$2a$11$SeedHashOrganiser000000002', N'Lerato',  N'Dlamini',   N'0837654321'),
    (@ParticipantRoleId, N'sipho.khumalo@gmail.com',          N'$2a$11$SeedHashParticipant00000001', N'Sipho',   N'Khumalo',   N'0713334444'),
    (@ParticipantRoleId, N'anele.naidoo@outlook.com',         N'$2a$11$SeedHashParticipant00000002', N'Anele',   N'Naidoo',    N'0725556666'),
    (@ParticipantRoleId, N'johan.vanwyk@webmail.co.za',       N'$2a$11$SeedHashParticipant00000003', N'Johan',   N'van Wyk',   N'0617778888'),
    (@ParticipantRoleId, N'nomsa.mahlangu@gmail.com',         N'$2a$11$SeedHashParticipant00000004', N'Nomsa',   N'Mahlangu',  N'0829990000');
GO

INSERT INTO dbo.ParticipantProfile
    (UserId, DateOfBirth, Gender, ClubName, TShirtSize, EmergencyContactName, EmergencyContactPhone, MedicalNotes)
SELECT UserId, D.DateOfBirth, D.Gender, D.ClubName, D.TShirtSize, D.ContactName, D.ContactPhone, D.Notes
FROM dbo.AppUser U
JOIN (VALUES
        (N'sipho.khumalo@gmail.com',    '1994-03-12', N'Male',   N'Soweto Striders AC',     N'M',  N'Zanele Khumalo', N'0715551212', NULL),
        (N'anele.naidoo@outlook.com',   '1989-11-02', N'Female', N'Durban Athletic Club',   N'S',  N'Ravi Naidoo',    N'0824448899', N'Mild asthma - carries inhaler.'),
        (N'johan.vanwyk@webmail.co.za', '1977-06-25', N'Male',   N'Pretoria Wheelers CC',   N'XL', N'Marie van Wyk',  N'0833219876', NULL),
        (N'nomsa.mahlangu@gmail.com',   '2001-01-30', N'Female', N'Tshwane Runners',        N'M',  N'Peter Mahlangu', N'0761239876', NULL)
     ) AS D(Email, DateOfBirth, Gender, ClubName, TShirtSize, ContactName, ContactPhone, Notes)
  ON D.Email = U.Email;
GO

DECLARE @Thabo  INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'thabo.molefe@comradesrace.co.za');
DECLARE @Lerato INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'lerato.dlamini@ctcycletour.co.za');

INSERT INTO dbo.Event
    (OrganiserId, Name, Description, EventType, EventDate, VenueName, City, Province,
     Latitude, Longitude, RegistrationOpensOn, RegistrationClosesOn, Status)
VALUES
    (@Thabo,  N'Soweto Marathon 2026',
              N'South Africa''s toughest marathon through the historic streets of Soweto, taking in Orlando Stadium, the Hector Pieterson Memorial and Vilakazi Street.',
              N'Run',   '2026-11-01', N'Nasrec Expo Centre', N'Johannesburg', N'Gauteng',
              -26.240000, 27.983000, '2026-06-01', '2026-10-20', N'Published'),

    (@Lerato, N'Cape Town Cycle Tour 2026',
              N'The world''s largest timed cycle race, a 109 km loop around the Cape Peninsula including Chapman''s Peak and Suikerbossie.',
              N'Cycle', '2026-03-08', N'Grand Parade', N'Cape Town', N'Western Cape',
              -33.925800, 18.423200, '2025-10-01', '2026-02-20', N'Published'),

    (@Thabo,  N'Durban Beachfront Charity Walk 2026',
              N'A family-friendly walk along the Golden Mile promenade raising funds for local school sports programmes.',
              N'Walk',  '2026-09-26', N'uShaka Marine World', N'Durban', N'KwaZulu-Natal',
              -29.867000, 31.044000, '2026-05-01', '2026-09-20', N'Published');
GO

DECLARE @Soweto INT = (SELECT EventId FROM dbo.Event WHERE Name = N'Soweto Marathon 2026');
DECLARE @CTCT   INT = (SELECT EventId FROM dbo.Event WHERE Name = N'Cape Town Cycle Tour 2026');
DECLARE @Walk   INT = (SELECT EventId FROM dbo.Event WHERE Name = N'Durban Beachfront Charity Walk 2026');

INSERT INTO dbo.EventCategory
    (EventId, Name, DistanceKm, EntryFee, StartTime, MaxParticipants, MinimumAge, CutOffMinutes)
VALUES
    (@Soweto, N'Full Marathon 42.2 km',  42.20, 450.00, '06:00', 12000, 20, 360),
    (@Soweto, N'Half Marathon 21.1 km',  21.10, 300.00, '06:30',  8000, 16, 210),
    (@Soweto, N'Fun Run 10 km',          10.00, 150.00, '07:00',  5000,  0, 120),

    (@CTCT,   N'Race Route 109 km',     109.00, 850.00, '06:15', 30000, 18, 480),
    (@CTCT,   N'Lite Route 42 km',       42.00, 480.00, '08:00',  4000, 12, 240),

    (@Walk,   N'Charity Walk 5 km',       5.00,  80.00, '08:00',  2500,  0, NULL),
    (@Walk,   N'Charity Walk 10 km',     10.00, 120.00, '07:30',  1500, 12, 150);
GO

DECLARE @SowetoFull INT = (SELECT CategoryId FROM dbo.EventCategory
                           WHERE Name = N'Full Marathon 42.2 km');
DECLARE @Walk5      INT = (SELECT CategoryId FROM dbo.EventCategory
                           WHERE Name = N'Charity Walk 5 km');

INSERT INTO dbo.RouteWaypoint
    (CategoryId, SequenceNumber, Name, WaypointType, DistanceMarkerKm, Latitude, Longitude)
VALUES
    (@SowetoFull, 1, N'Nasrec Start Chute',      N'Start',        0.00,  -26.240000, 27.983000),
    (@SowetoFull, 2, N'Orlando Stadium',         N'WaterPoint',  12.50,  -26.267000, 27.929000),
    (@SowetoFull, 3, N'Vilakazi Street',         N'Marker',      21.10,  -26.238000, 27.907000),
    (@SowetoFull, 4, N'Hector Pieterson Memorial', N'MedicalPoint', 30.00, -26.233000, 27.909000),
    (@SowetoFull, 5, N'Nasrec Finish',           N'Finish',      42.20,  -26.240000, 27.983000),

    (@Walk5,      1, N'uShaka Start',            N'Start',        0.00,  -29.867000, 31.044000),
    (@Walk5,      2, N'Suncoast Turnaround',     N'Marker',       2.50,  -29.843000, 31.041000),
    (@Walk5,      3, N'uShaka Finish',           N'Finish',       5.00,  -29.867000, 31.044000);
GO

INSERT INTO dbo.Enrolment (EventId, CategoryId, ParticipantId, RaceNumber, Status, AmountPaid, PaymentReference)
SELECT C.EventId, C.CategoryId, U.UserId, D.RaceNumber, D.Status, D.AmountPaid, D.PaymentRef
FROM (VALUES
        (N'Soweto Marathon 2026',                 N'Full Marathon 42.2 km', N'sipho.khumalo@gmail.com',    N'A1042', N'Confirmed', 450.00, N'PAY-2026-000101'),
        (N'Soweto Marathon 2026',                 N'Half Marathon 21.1 km', N'nomsa.mahlangu@gmail.com',   N'B2087', N'Confirmed', 300.00, N'PAY-2026-000102'),
        (N'Soweto Marathon 2026',                 N'Fun Run 10 km',         N'anele.naidoo@outlook.com',   N'C3311', N'Confirmed', 150.00, N'PAY-2026-000103'),
        (N'Cape Town Cycle Tour 2026',            N'Race Route 109 km',     N'johan.vanwyk@webmail.co.za', N'D0455', N'Confirmed', 850.00, N'PAY-2026-000104'),
        (N'Cape Town Cycle Tour 2026',            N'Lite Route 42 km',      N'anele.naidoo@outlook.com',   N'E1120', N'Confirmed', 480.00, N'PAY-2026-000105'),
        (N'Durban Beachfront Charity Walk 2026',  N'Charity Walk 5 km',     N'nomsa.mahlangu@gmail.com',   N'F0012', N'Confirmed',  80.00, N'PAY-2026-000106'),
        (N'Durban Beachfront Charity Walk 2026',  N'Charity Walk 10 km',    N'sipho.khumalo@gmail.com',    N'G0301', N'Pending',     0.00, NULL)
     ) AS D(EventName, CategoryName, Email, RaceNumber, Status, AmountPaid, PaymentRef)
JOIN dbo.Event E         ON E.Name  = D.EventName
JOIN dbo.EventCategory C ON C.EventId = E.EventId AND C.Name = D.CategoryName
JOIN dbo.AppUser U       ON U.Email = D.Email;
GO

DECLARE @Thabo INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'thabo.molefe@comradesrace.co.za');

INSERT INTO dbo.Result (EnrolmentId, CapturedByUserId, FinishTimeSeconds, OverallPosition, CategoryPosition, Status)
SELECT EN.EnrolmentId, @Thabo, D.FinishSeconds, D.OverallPos, D.CategoryPos, D.Status
FROM (VALUES
        (N'A1042', 13860, 42,  42,  N'Finished'),   /* 03:51:00 */
        (N'B2087',  7320, 15,   4,  N'Finished'),   /* 02:02:00 */
        (N'C3311',  3540, 88,  88,  N'Finished'),   /* 00:59:00 */
        (N'D0455', 15300, 610, 610, N'Finished'),   /* 04:15:00 */
        (N'E1120',  NULL, NULL, NULL, N'DNF')
     ) AS D(RaceNumber, FinishSeconds, OverallPos, CategoryPos, Status)
JOIN dbo.Enrolment EN ON EN.RaceNumber = D.RaceNumber;
GO

INSERT INTO dbo.EventMedia (EventId, UploadedByUserId, MediaType, FileName, BlobUri, ContentType, SizeInBytes)
SELECT E.EventId, E.OrganiserId, D.MediaType, D.FileName, D.BlobUri, D.ContentType, D.SizeInBytes
FROM (VALUES
        (N'Soweto Marathon 2026',                N'Banner',   N'soweto-2026-banner.jpg',   N'https://racedaystorage.blob.core.windows.net/event-media/soweto-2026-banner.jpg',   N'image/jpeg', 482311),
        (N'Soweto Marathon 2026',                N'RouteMap', N'soweto-2026-route.png',    N'https://racedaystorage.blob.core.windows.net/event-media/soweto-2026-route.png',    N'image/png',  921044),
        (N'Cape Town Cycle Tour 2026',           N'Banner',   N'ctct-2026-banner.jpg',     N'https://racedaystorage.blob.core.windows.net/event-media/ctct-2026-banner.jpg',     N'image/jpeg', 517820),
        (N'Durban Beachfront Charity Walk 2026', N'Document', N'durban-walk-indemnity.pdf',N'https://racedaystorage.blob.core.windows.net/event-media/durban-walk-indemnity.pdf',N'application/pdf', 145602)
     ) AS D(EventName, MediaType, FileName, BlobUri, ContentType, SizeInBytes)
JOIN dbo.Event E ON E.Name = D.EventName;
GO

SELECT 'Role' AS TableName, COUNT(*) AS [RowCount] FROM dbo.Role
UNION ALL SELECT 'AppUser',            COUNT(*) FROM dbo.AppUser
UNION ALL SELECT 'ParticipantProfile', COUNT(*) FROM dbo.ParticipantProfile
UNION ALL SELECT 'Event',              COUNT(*) FROM dbo.Event
UNION ALL SELECT 'EventCategory',      COUNT(*) FROM dbo.EventCategory
UNION ALL SELECT 'RouteWaypoint',      COUNT(*) FROM dbo.RouteWaypoint
UNION ALL SELECT 'Enrolment',          COUNT(*) FROM dbo.Enrolment
UNION ALL SELECT 'Result',             COUNT(*) FROM dbo.Result
UNION ALL SELECT 'EventMedia',         COUNT(*) FROM dbo.EventMedia;

SELECT  E.Name          AS EventName,
        E.EventDate,
        E.City,
        E.Province,
        O.FirstName + ' ' + O.LastName AS Organiser,
        COUNT(EN.EnrolmentId)          AS Entries,
        SUM(EN.AmountPaid)             AS RevenueZAR
FROM dbo.Event E
JOIN dbo.AppUser O       ON O.UserId = E.OrganiserId
LEFT JOIN dbo.Enrolment EN ON EN.EventId = E.EventId AND EN.Status = N'Confirmed'
GROUP BY E.Name, E.EventDate, E.City, E.Province, O.FirstName, O.LastName
ORDER BY E.EventDate;

SELECT  U.FirstName + ' ' + U.LastName AS Participant,
        E.Name                         AS EventName,
        C.Name                         AS Category,
        C.DistanceKm,
        EN.RaceNumber,
        R.Status,
        CASE WHEN R.FinishTimeSeconds IS NULL THEN NULL
             ELSE RIGHT('0' + CAST(R.FinishTimeSeconds / 3600 AS VARCHAR(2)), 2) + ':' +
                  RIGHT('0' + CAST((R.FinishTimeSeconds % 3600) / 60 AS VARCHAR(2)), 2) + ':' +
                  RIGHT('0' + CAST(R.FinishTimeSeconds % 60 AS VARCHAR(2)), 2)
        END                            AS FinishTime,
        R.CategoryPosition
FROM dbo.Enrolment EN
JOIN dbo.AppUser U       ON U.UserId     = EN.ParticipantId
JOIN dbo.Event E         ON E.EventId    = EN.EventId
JOIN dbo.EventCategory C ON C.CategoryId = EN.CategoryId
LEFT JOIN dbo.Result R   ON R.EnrolmentId = EN.EnrolmentId
ORDER BY Participant, E.EventDate;

SELECT  C.Name AS Category,
        R.CategoryPosition,
        U.FirstName + ' ' + U.LastName AS Participant,
        R.FinishTimeSeconds
FROM dbo.Result R
JOIN dbo.Enrolment EN    ON EN.EnrolmentId = R.EnrolmentId
JOIN dbo.EventCategory C ON C.CategoryId   = EN.CategoryId
JOIN dbo.Event E         ON E.EventId      = EN.EventId
JOIN dbo.AppUser U       ON U.UserId       = EN.ParticipantId
WHERE E.Name = N'Soweto Marathon 2026'
ORDER BY C.DistanceKm DESC, R.CategoryPosition;
GO

