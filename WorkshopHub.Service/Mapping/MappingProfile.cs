using AutoMapper;
using WorkshopHub.Core.DTOs.Categories;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.DTOs.Tags;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Service.Mapping;

/// <summary>
/// כל המיפויים בין ישויות ל-DTOs. הישויות עצמן לא יוצאות מה-API ולא נכנסות אליו:
/// בקשה נכנסת ממופה מ-Request DTO לישות, ותשובה יוצאת ממופה מישות ל-Response DTO.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // ---------- ישות -> Response ----------
        CreateMap<User, UserResponse>();
        CreateMap<Category, CategoryResponse>();
        CreateMap<Tag, TagResponse>();

        // CategoryName מתמלא אוטומטית מ-Category.Name (flattening לפי שם)
        CreateMap<Workshop, WorkshopSummaryResponse>();
        CreateMap<Workshop, WorkshopDetailsResponse>();

        CreateMap<Session, SessionSummaryResponse>();
        CreateMap<Session, SessionResponse>()
            .ForMember(d => d.InstructorName, o => o.MapFrom(s => s.Workshop.InstructorName))
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Workshop.Category.Name))
            // מצב ההרשמה של המשתמשת המחוברת לא שייך לישות – ה-Service ממלא אותו
            .ForMember(d => d.MyRegistrationId, o => o.Ignore())
            .ForMember(d => d.MyRegistrationStatus, o => o.Ignore());

        CreateMap<Registration, RegistrationResponse>()
            .ForMember(d => d.WorkshopId, o => o.MapFrom(r => r.Session.WorkshopId))
            .ForMember(d => d.WorkshopTitle, o => o.MapFrom(r => r.Session.Workshop.Title))
            .ForMember(d => d.Location, o => o.MapFrom(r => r.Session.Location));

        // ---------- Request -> ישות ----------
        // MemberList.Source: הוולידציה של המיפוי בודקת שכל שדה ב-Request מגיע ליעד.
        // שדות של הישות שלא מגיעים מהלקוח (Id, מונים, RowVersion, קשרים) נשארים בשליטת ה-Service.
        CreateMap<CategoryRequest, Category>(MemberList.Source);
        CreateMap<TagRequest, Tag>(MemberList.Source);

        CreateMap<WorkshopRequest, Workshop>(MemberList.Source)
            // התגיות נטענות מבסיס הנתונים לפי TagIds ומשויכות ב-Service
            .ForSourceMember(s => s.TagIds, o => o.DoNotValidate());

        CreateMap<SessionCreateRequest, Session>(MemberList.Source);
        CreateMap<SessionUpdateRequest, Session>(MemberList.Source)
            // ה-RowVersion מהלקוח לא נכתב לישות; הוא נקבע כ"ערך המקורי" לבדיקת ההתנגשות
            .ForMember(d => d.RowVersion, o => o.Ignore())
            .ForSourceMember(s => s.RowVersion, o => o.DoNotValidate());
    }
}
