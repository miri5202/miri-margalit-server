using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Unit;

public class MappingProfileTests
{
    [Fact]
    public void Configuration_IsValid_EveryDtoMemberHasASource()
    {
        // נכשל אם נוסף ל-Response DTO שדה שאין לו מקור בישות, או שדה ב-Request DTO שלא מגיע לישות
        TestMapper.Configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void Session_MapsToResponse_WithWorkshopDetailsAndComputedSeats()
    {
        var session = new Session
        {
            Id = 10,
            WorkshopId = 3,
            Workshop = new Workshop
            {
                Id = 3,
                Title = "קליגרפיה עברית",
                InstructorName = "אסתר וייס",
                Category = new Category { Id = 2, Name = "מלאכת יד ויצירה" }
            },
            StartsAtUtc = new DateTime(2026, 12, 1, 17, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 120,
            Location = "חדר יצירה 2",
            Capacity = 10,
            SeatsTaken = 10,
            WaitlistCount = 2,
            RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 }
        };

        var response = TestMapper.Instance.Map<SessionResponse>(session);

        Assert.Equal("קליגרפיה עברית", response.WorkshopTitle);
        Assert.Equal("אסתר וייס", response.InstructorName);
        Assert.Equal("מלאכת יד ויצירה", response.CategoryName);
        Assert.Equal(0, response.SeatsLeft);
        Assert.True(response.IsFull);
        Assert.Equal(2, response.WaitlistCount);
        Assert.Equal(session.RowVersion, response.RowVersion);
        Assert.Null(response.MyRegistrationId);
    }

    [Fact]
    public void User_MapsToResponse_WithoutExposingThePasswordHash()
    {
        // ל-UserResponse פשוט אין שדה כזה: ה-DTO הוא מה שמבטיח שהגיבוב לא יוצא מה-API
        Assert.Null(typeof(UserResponse).GetProperty(nameof(User.PasswordHash)));

        var response = TestMapper.Instance.Map<UserResponse>(new User
        {
            Id = 7,
            FullName = "רחל כהן",
            Email = "rachel@example.com",
            PasswordHash = "PBKDF2-SHA256$100000$salt$hash",
            Role = UserRole.Member
        });

        Assert.Equal(7, response.Id);
        Assert.Equal(UserRole.Member, response.Role);
    }

    [Fact]
    public void WorkshopRequest_MapsOntoExistingWorkshop_WithoutTouchingIdTagsOrSessions()
    {
        var tag = new Tag { Id = 1, Name = "ערב" };
        var workshop = new Workshop
        {
            Id = 5,
            Title = "ישן",
            CategoryId = 1,
            Tags = { tag },
            Sessions = { new Session { Id = 10 } }
        };

        TestMapper.Instance.Map(
            new WorkshopRequest { Title = "חדש", Description = "תיאור", InstructorName = "מנחה", CategoryId = 2, TagIds = { 9 } },
            workshop);

        Assert.Equal(5, workshop.Id);
        Assert.Equal("חדש", workshop.Title);
        Assert.Equal(2, workshop.CategoryId);
        Assert.Same(tag, Assert.Single(workshop.Tags));
        Assert.Single(workshop.Sessions);
    }

    [Fact]
    public void SessionUpdateRequest_MapsOntoExistingSession_WithoutOverwritingCountersOrRowVersion()
    {
        var storedRowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 7, 210 };
        var session = new Session { Id = 10, Capacity = 5, SeatsTaken = 4, WaitlistCount = 1, RowVersion = storedRowVersion };

        TestMapper.Instance.Map(
            new SessionUpdateRequest
            {
                StartsAtUtc = new DateTime(2026, 12, 1, 17, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 90,
                Location = "אולם הרצאות",
                Capacity = 8,
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 7, 209 }
            },
            session);

        Assert.Equal(8, session.Capacity);
        Assert.Equal("אולם הרצאות", session.Location);
        Assert.Equal(4, session.SeatsTaken);
        Assert.Equal(1, session.WaitlistCount);
        Assert.Same(storedRowVersion, session.RowVersion);
    }

    [Fact]
    public void Registration_MapsToResponse_WithSessionAndUserDetails()
    {
        var registration = new Registration
        {
            Id = 50,
            SessionId = 10,
            Session = new Session
            {
                Id = 10,
                WorkshopId = 3,
                Workshop = new Workshop { Id = 3, Title = "קליגרפיה עברית" },
                StartsAtUtc = new DateTime(2026, 12, 1, 17, 0, 0, DateTimeKind.Utc),
                Location = "חדר יצירה 2"
            },
            UserId = 7,
            User = new User { Id = 7, FullName = "רחל כהן" },
            Status = RegistrationStatus.Waitlisted
        };

        var response = TestMapper.Instance.Map<RegistrationResponse>(registration);

        Assert.Equal(3, response.WorkshopId);
        Assert.Equal("קליגרפיה עברית", response.WorkshopTitle);
        Assert.Equal("חדר יצירה 2", response.Location);
        Assert.Equal("רחל כהן", response.UserFullName);
        Assert.Equal(registration.Session.StartsAtUtc, response.SessionStartsAtUtc);
        Assert.Equal(RegistrationStatus.Waitlisted, response.Status);
    }
}
