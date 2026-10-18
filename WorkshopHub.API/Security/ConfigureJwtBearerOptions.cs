using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkshopHub.API.Extensions;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.Settings;

namespace WorkshopHub.API.Security;

/// <summary>
/// הגדרות האימות של ה-JWT בצד השרת. נבנות מ-<see cref="JwtSettings"/> (IOptions), כך שהמפתח מגיע
/// מהקונפיגורציה (User Secrets) ולא מהקוד.
/// </summary>
public class ConfigureJwtBearerOptions : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtSettings _jwt;

    public ConfigureJwtBearerOptions(IOptions<JwtSettings> jwtOptions)
    {
        _jwt = jwtOptions.Value;
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        // משאירים את שמות ה-claims כמו שהם ב-token (sub, role...) בלי המרה לשמות הארוכים של .NET
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AppClaimTypes.Name,
            // [Authorize(Roles = "Admin")] בודק את ה-claim הזה
            RoleClaimType = AppClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            // 401: אין token, או שהוא לא תקין / פג תוקף. מחזירים את מבנה השגיאה האחיד במקום גוף ריק.
            OnChallenge = async context =>
            {
                context.HandleResponse();

                if (context.Response.HasStarted)
                    return;

                context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                await ErrorResponseFactory.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    ErrorCodes.Unauthorized,
                    "נדרשת התחברות: ה-token חסר, אינו תקין או שפג תוקפו.");
            },

            // 403: המשתמש מזוהה, אבל התפקיד שלו לא מספיק לפעולה
            OnForbidden = context => ErrorResponseFactory.WriteAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                ErrorCodes.Forbidden,
                "אין לך הרשאה לבצע את הפעולה הזו.")
        };
    }
}
