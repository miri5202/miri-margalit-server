using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using WorkshopHub.Service.Mapping;

namespace WorkshopHub.Tests.Helpers;

/// <summary>IMapper אמיתי עם ה-Profile של המערכת. המיפוי הוא חלק מההתנהגות שנבדקת, ולכן לא מדמים אותו.</summary>
internal static class TestMapper
{
    public static MapperConfiguration Configuration { get; } =
        new(configuration => configuration.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);

    public static IMapper Instance { get; } = Configuration.CreateMapper();
}
