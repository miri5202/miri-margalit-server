using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using NLog.Web;
using WorkshopHub.API.Extensions;
using WorkshopHub.API.Middleware;
using WorkshopHub.API.Security;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;
using WorkshopHub.Core.Settings;
using WorkshopHub.Data;
using WorkshopHub.Data.Repositories;
using WorkshopHub.Data.Seed;
using WorkshopHub.Service.Mapping;
using WorkshopHub.Service.Security;
using WorkshopHub.Service.Services;

// NLog נטען לפני כל דבר אחר, כדי שגם כשל בעליית השרת ייכתב ללוג
NLog.LogManager.Setup().LoadConfigurationFromAppSettings();
var startupLogger = NLog.LogManager.GetLogger("WorkshopHub.API.Startup");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // =====================================================================
    //  Logging – הקוד משתמש ב-ILogger הרגיל של .NET, ו-NLog מחובר מתחת
    // =====================================================================
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.Trace); // הסינון לפי רמות נעשה ב-nlog.config
    builder.Host.UseNLog();

    // רמת הלוג המינימלית של האפליקציה מגיעה מהקונפיגורציה: Debug בפיתוח, Info בשאר הסביבות
    var appMinLevel = builder.Configuration["AppLogging:MinLevel"];
    if (!string.IsNullOrWhiteSpace(appMinLevel) && NLog.LogManager.Configuration is not null)
    {
        NLog.LogManager.Configuration.Variables["appMinLevel"] = appMinLevel;
        NLog.LogManager.ReconfigExistingLoggers();
    }

    // בודק בזמן עליית השרת ש-singleton לא מחזיק שירות scoped, ושכל התלויות הרשומות ניתנות לבנייה
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });

    // =====================================================================
    //  Configuration – IOptions עם ולידציה בעליית השרת
    // =====================================================================
    // Jwt:Key הוא סוד ואינו נמצא ב-repo: בפיתוח הוא מגיע מ-User Secrets. אם הוא חסר, השרת לא עולה.
    builder.Services.AddOptions<JwtSettings>()
        .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));

    // =====================================================================
    //  Data – DbContext ו-Repositories
    //  (המקום היחיד שבו שכבת ה-API מכירה את שכבת ה-Data: רישום השירותים)
    // =====================================================================
    // ה-connection string נטען מהקונפיגורציה (appsettings.Development.json או User Secrets), לא מהקוד.
    // DbContext הוא scoped: מופע אחד לכל בקשת HTTP. כל ה-repositories של אותה בקשה חולקים אותו,
    // ולכן SaveChanges אחד שומר את כל השינויים שלהם בטרנזקציה אחת. הוא אינו thread-safe,
    // ולכן אסור שישותף בין בקשות.
    // בסיס הנתונים הוא PostgreSQL (ספריית Npgsql). ה-connection string יכול להגיע גם בצורת URL
    // (postgresql://user:password@host/db) כמו שמציג Render – הוא מומר לצורה ש-Npgsql מבין.
    var connectionString = PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Default"));
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

    // Repositories ו-UnitOfWork הם scoped כי הם מחזיקים את ה-DbContext, שהוא scoped
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
    builder.Services.AddScoped<ITagRepository, TagRepository>();
    builder.Services.AddScoped<IWorkshopRepository, WorkshopRepository>();
    builder.Services.AddScoped<ISessionRepository, SessionRepository>();
    builder.Services.AddScoped<IRegistrationRepository, RegistrationRepository>();
    builder.Services.AddScoped<IDbInitializer, DbInitializer>();

    // =====================================================================
    //  Services – הלוגיקה העסקית
    // =====================================================================
    // scoped: כל Service מקבל repositories שהם scoped. אילו Service היה singleton, הוא היה "תופס"
    // את ה-DbContext של הבקשה הראשונה ומשתמש בו לנצח – הבאג של singleton שמחזיק scoped.
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<ICategoryService, CategoryService>();
    builder.Services.AddScoped<ITagService, TagService>();
    builder.Services.AddScoped<IWorkshopService, WorkshopService>();
    builder.Services.AddScoped<ISessionService, SessionService>();
    builder.Services.AddScoped<IRegistrationService, RegistrationService>();

    // singleton: שירותים בלי מצב ובלי תלות ב-scoped – מופע אחד מספיק לכל האפליקציה
    builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
    builder.Services.AddSingleton<ITokenService, JwtTokenService>();
    builder.Services.AddSingleton(TimeProvider.System);

    // AutoMapper – טוען את כל ה-Profiles משכבת ה-Service.
    // מפתח הרישיון אופציונלי: בלעדיו AutoMapper עובד כרגיל וכותב אזהרה ללוג בעליית השרת.
    builder.Services.AddAutoMapper(
        configuration =>
        {
            var licenseKey = builder.Configuration["AutoMapper:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
                configuration.LicenseKey = licenseKey;
        },
        typeof(MappingProfile).Assembly);

    // =====================================================================
    //  Authentication & Authorization – JWT
    // =====================================================================
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
    builder.Services.ConfigureOptions<ConfigureJwtBearerOptions>();
    builder.Services.AddAuthorization();

    // =====================================================================
    //  Web API – Controllers, JSON, ולידציה, CORS, Swagger
    // =====================================================================
    var hebrewFriendlyEncoder = JavaScriptEncoder.Create(UnicodeRanges.All);

    builder.Services.AddControllers()
        .ConfigureApiBehaviorOptions(options =>
        {
            // ModelState לא תקין -> 400 במבנה השגיאה האחיד
            options.InvalidModelStateResponseFactory = ErrorResponseFactory.FromInvalidModelState;
        })
        .AddJsonOptions(options =>
        {
            // enums יוצאים ונכנסים כטקסט ("Confirmed") ולא כמספר
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.Encoder = hebrewFriendlyEncoder;
        });

    // אותן הגדרות גם לתשובות שנכתבות מחוץ ל-MVC (ה-middleware של השגיאות, אירועי ה-JWT)
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.SerializerOptions.Encoder = hebrewFriendlyEncoder;
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    builder.Services.AddCors(options =>
        options.AddPolicy("Client", policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Location")));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "WorkshopHub API",
            Version = "v1",
            Description = "הרשמה לסדנאות עם מכסת מקומות ורשימת המתנה. " +
                          "להתחברות: POST /api/tokens, ואז כפתור Authorize עם ה-accessToken שהתקבל."
        });

        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "הדביקי כאן את ה-accessToken (בלי המילה Bearer)."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = JwtBearerDefaults.AuthenticationScheme
                    }
                },
                Array.Empty<string>()
            }
        });

        var xmlDocumentation = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
        if (File.Exists(xmlDocumentation))
            options.IncludeXmlComments(xmlDocumentation);
    });

    var app = builder.Build();

    // =====================================================================
    //  אתחול בסיס הנתונים – כדי שהמערכת תרוץ בלי שום פעולה ידנית
    // =====================================================================
    // קריאה ל-Value מפעילה את הוולידציה של JwtSettings כבר כאן, עם הודעה ברורה אם Jwt:Key חסר
    _ = app.Services.GetRequiredService<IOptions<JwtSettings>>().Value;

    var databaseSettings = app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    if (databaseSettings.ApplyMigrationsOnStartup || databaseSettings.SeedDemoData)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is missing. Set it in appsettings.Development.json or with: " +
                "dotnet user-secrets set \"ConnectionStrings:Default\" \"<connection string>\" --project WorkshopHub.API");
        }

        // אבחון: לפני המיגרציות בודקים אם שרת בסיס הנתונים בכלל נגיש ברשת (DNS + פורט),
        // כדי שבענן תהיה הודעה ברורה במקום חריגת רשת כללית
        await LogDatabaseReachabilityAsync(connectionString, startupLogger);

        // IDbInitializer הוא scoped (הוא מחזיק DbContext), ולכן פותחים scope ידני – אין כאן בקשת HTTP
        await using var scope = app.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        await initializer.InitializeAsync(databaseSettings.ApplyMigrationsOnStartup, databaseSettings.SeedDemoData);
    }

    // =====================================================================
    //  HTTP pipeline – הסדר חשוב
    // =====================================================================
    // 1. טיפול בשגיאות – ראשון, כדי לעטוף את כל מה שבא אחריו
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // 2. CorrelationId – לפני כל middleware שכותב לוגים
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 3. לוג לכל בקשה נכנסת
    app.UseMiddleware<RequestLoggingMiddleware>();

    // Swagger: תמיד בפיתוח. בשרת המפורסם רק אם Swagger:Enabled=true (כדי שאפשר יהיה להציג את ה-API)
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "WorkshopHub API v1"));
    }

    // הפניה ל-HTTPS: ברירת המחדל – בכל סביבה חוץ מפיתוח. אפשר לכבות (Https:Redirect=false)
    // באחסון שעדיין אין בו תעודת HTTPS, אחרת כל בקשה תופנה לכתובת שלא עונה.
    if (app.Configuration.GetValue("Https:Redirect", !app.Environment.IsDevelopment()))
    {
        app.UseHttpsRedirection();
    }

    // האתר המפורסם: אם קיימת תיקיית wwwroot (הלקוח אחרי npm run build), השרת מגיש גם אותו.
    // כך הלקוח וה-API יושבים באותה כתובת, בלי CORS. בפיתוח התיקייה לא קיימת והשורות לא פועלות.
    if (Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot")))
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();
    }

    app.UseCors("Client");

    // 4. authentication (מי את?) לפני authorization (מה מותר לך?)
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    await app.RunAsync();

    // כותב ללוג לאיזו כתובת שרת בסיס הנתונים מתורגם ואם אפשר לפתוח אליו חיבור TCP.
    // לא כותב את ה-connection string עצמו – יש בו סיסמה.
    static async Task LogDatabaseReachabilityAsync(string connectionString, NLog.Logger logger)
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        var host = settings.Host?.Split(',')[0].Trim() ?? "localhost";
        var port = settings.Port;

        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(host);
            logger.Info("Database server {0}:{1} resolves to {2}", host, port, string.Join(", ", addresses.Select(a => a.ToString())));

            using var tcpClient = new System.Net.Sockets.TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await tcpClient.ConnectAsync(host, port, timeout.Token);
            logger.Info("Database server {0}:{1} is reachable (TCP connection succeeded)", host, port);
        }
        catch (Exception exception) when (exception is System.Net.Sockets.SocketException or OperationCanceledException)
        {
            logger.Warn("Database server {0}:{1} is NOT reachable from this machine: {2}", host, port, exception.Message);
        }
    }
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    // HostAbortedException נזרקת בכוונה על ידי כלי ה-EF Core (dotnet ef) כשהם טוענים את ה-host – היא לא כשל
    startupLogger.Error(exception, "WorkshopHub API stopped because of an unhandled exception during startup");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}
