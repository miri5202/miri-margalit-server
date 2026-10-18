<div dir="rtl">

# WorkshopHub – הרשמה לסדנאות עם מכסת מקומות

פרויקט סיום בקורס ‎.NET‏ – שרת ASP.NET Core Web API.
הלקוח (React) נמצא ב-repository נפרד: `workshophub-client`.

## מה המערכת עושה

WorkshopHub היא מערכת הרשמה לסדנאות. כל סדנה (למשל "אפיית חלות ולחמים") מתקיימת במפגשים, ולכל מפגש יש מספר מקומות מוגבל. משתתפות נרשמות למפגש; כשהמפגש מתמלא אפשר להצטרף לרשימת המתנה, וכשמישהי מבטלת – הממתינה הוותיקה ביותר מקבלת את המקום שהתפנה באופן אוטומטי. מנהלות מנהלות את הסדנאות, המפגשים, הקטגוריות והתגיות, ורואות את כל ההרשמות.

## המשאב המוגבל והתחרות עליו

**המשאב המוגבל** הוא מקום במפגש (`Session`). **התחרות** קורית בפעולת ההרשמה, `POST /api/sessions/{sessionId}/registrations`: נשאר מקום אחד, ושתי משתתפות לוחצות "הרשמה" באותו רגע. שתי הבקשות קוראות את המפגש כשיש בו עדיין מקום פנוי, שתיהן עוברות את הבדיקה – ובלי הגנה, שתיהן היו מקבלות אישור על אותו מקום.

**איך המערכת מגיבה:** המערכת משתמשת ב-optimistic concurrency. לישות `Session` יש concurrency token – העמודה `RowVersion` מסוג `rowversion`, ש-SQL Server מעדכן בכל שינוי בשורה. EF Core מוסיף את הערך שנקרא לתנאי ה-`WHERE` של ה-`UPDATE`, כך שהבקשה השנייה לא מוצאת שורה לעדכן. כל השמירה שלה מתבטלת (גם ההרשמה שניסתה להוסיף – הכול באותה טרנזקציה), ב-EF Core נזרקת `DbUpdateConcurrencyException`, והלקוח מקבל **409 Conflict** עם הקוד `concurrency_conflict` וההודעה "המקום נתפס על ידי משתתפת אחרת ממש ברגע זה. רענני את הרשימה ונסי שוב". ההתנגשות נכתבת ללוג ברמת Warning.

### הפרטים

**ה-token מוגדר בקוד** – `WorkshopHub.Core/Entities/Session.cs` (התכונה `[Timestamp]`) ו-`WorkshopHub.Data/Configurations/SessionConfiguration.cs` (`IsRowVersion()`).

**המסלול של ההתנגשות בין השכבות:**

| שכבה | מה קורה | קובץ |
|---|---|---|
| Data | תופסת `DbUpdateConcurrencyException` (חריגה ספציפית, לא `Exception`) ומתרגמת אותה ל-`ConcurrencyConflictException` של Core | `Repositories/UnitOfWork.cs` |
| Service | תופסת `ConcurrencyConflictException`, כותבת Warning ללוג ומחזירה `Result` מסוג Conflict | `Services/RegistrationService.cs` |
| API | מתרגמת Conflict ל-409 במבנה השגיאה האחיד | `Controllers/ApiControllerBase.cs` |

התרגום בשכבת ה-Data קיים כדי ששכבת ה-Service לא תצטרך להכיר את EF Core: היא מפנה ל-Core בלבד.

**הבדיקה והשמירה באותה טרנזקציה.** ‏`SaveChangesAsync` אחד שומר את ה-`UPDATE` של המפגש ואת ה-`INSERT` של ההרשמה יחד. הבדיקה שבאמת קובעת – "האם המפגש עדיין במצב שקראתי?" – היא תנאי ה-`WHERE` של ה-`UPDATE`, והיא רצה בתוך הטרנזקציה. הבדיקה בקוד שלפני השמירה נועדה רק להחזיר הודעה מדויקת במקרה הרגיל.

**כל פעולה על הרשמות המפגש משנה את שורת המפגש.** הרשמה מעדכנת את `SeatsTaken`, והצטרפות לרשימת ההמתנה או עזיבה שלה מעדכנות את `WaitlistCount`. לכן כל שתי פעולות מקבילות על אותו מפגש – הרשמה מול הרשמה, הרשמה מול ביטול, ביטול מול הצטרפות להמתנה – מתנגשות על אותו token, ואחת מהן נדחית. בלי המונה `WaitlistCount`, הצטרפות להמתנה לא הייתה נוגעת בשורת המפגש, ומשתתפת הייתה יכולה להיכנס להמתנה בדיוק ברגע שמקום התפנה.

**התחרות קיימת גם בעריכה.** ב-`PUT /api/sessions/{id}` הלקוח מחזיר את ה-`rowVersion` שקיבל כשטען את המפגש. אם מישהי נרשמה מאז, העדכון נדחה ב-409 במקום לדרוס את השינוי שלה (`SessionService.UpdateAsync`, `ISessionRepository.SetOriginalRowVersion`).

**קווי הגנה נוספים בבסיס הנתונים** (למקרה של באג בקוד):
- `CHECK` constraint: ‏`SeatsTaken` בין 0 ל-`Capacity`.
- אינדקס ייחודי מסונן על `(SessionId, UserId)` להרשמות שאינן מבוטלות – למשתמשת יש לכל היותר הרשמה פעילה אחת למפגש, גם אם שלחה שתי בקשות במקביל.

### ההוכחה שזה עובד

`WorkshopHub.Tests/Concurrency/SessionConcurrencyTests.cs` – רץ מול SQL Server אמיתי (rowversion הוא מנגנון של בסיס הנתונים, ובסיס נתונים בזיכרון לא היה מוכיח כלום):

- **`TwoDbContexts_ChangeTheSameSession_FirstSaveSucceeds_SecondThrowsDbUpdateConcurrencyException`** – הבדיקה שנדרשת במדריך: שני `DbContext` נפרדים קוראים את אותו מפגש, שניהם משנים אותו, הראשון שומר בהצלחה והשני מקבל חריגה.
- `TwoUsers_RegisterForTheLastSeatAtTheSameMoment_ExactlyOneIsConfirmed_TheOtherGetsConflict` – אותו תרחיש דרך שכבת ה-Service, עם "שער" שמבטיח ששתי הבקשות קראו את המפגש לפני שאחת מהן שמרה.
- `ManyUsers_RaceForAFewSeats_TheSessionIsNeverOversold` – 12 משתתפות נרשמות בבת אחת ל-3 מקומות; המונה, ההרשמות בפועל ומספר האישורים זהים, ואף פעם לא מעל הקיבולת.
- `UpdatingASession_WithTheRowVersionLoadedBeforeSomeoneRegistered_IsRejected_ThenSucceedsWithTheFreshOne` – ה-token עושה סיבוב דרך הלקוח.

בנוסף, אפשר לראות את התחרות מול שרת שרץ:

```bash
node scripts/race-demo.mjs --users 8 --capacity 1
```

הסקריפט יוצר מפגש עם מקום אחד, רושם 8 משתתפות, שולח את כל בקשות ההרשמה באותו רגע ומדפיס מה כל אחת קיבלה: 201 אחד, והשאר 409.

## ארכיטקטורה

```
WorkshopHub.sln
  WorkshopHub.Core      Entities, Enums, DTOs, Interfaces, Result      – לא מפנה לשום פרויקט ולשום חבילה
  WorkshopHub.Data      DbContext, Fluent API, Repositories, Migrations – מפנה ל-Core
  WorkshopHub.Service   הלוגיקה העסקית, AutoMapper Profiles, JWT        – מפנה ל-Core
  WorkshopHub.API       Controllers, Middleware, Program.cs             – מפנה ל-Service ול-Core
  WorkshopHub.Tests     xUnit + Moq
```

- **Core** לא תלוי בשום דבר: אין בו אף `PackageReference` (אפשר לראות ב-`WorkshopHub.Core.csproj`).
- **API** מכיר את **Data** רק ב-`Program.cs`, לצורך רישום ה-DbContext וה-repositories. אף Controller לא מזריק `DbContext`.
- ה-interfaces של ה-repositories ושל ה-services מוגדרים ב-Core; המימושים ב-Data וב-Service; הרישום ב-`Program.cs`.
- ה-Controllers רזים: קושרים את הבקשה, קוראים ל-Service אחד, ומתרגמים את ה-`Result` לסטטוס HTTP.

### ישויות וקשרים

| ישות | תיאור | קשרים |
|---|---|---|
| `User` | משתמשת (Member / Admin) | one-to-many ל-`Registration` |
| `Category` | קטגוריה | one-to-many ל-`Workshop` |
| `Workshop` | סדנה | one-to-many ל-`Session`, ‏many-to-many ל-`Tag` |
| `Tag` | תגית | many-to-many ל-`Workshop` (טבלת `WorkshopTags`) |
| `Session` | מפגש – **המשאב המוגבל** | one-to-many ל-`Registration` |
| `Registration` | הרשמה (Confirmed / Waitlisted / Cancelled) | שייכת ל-`Session` ול-`User` |

## הרצה מקומית

### מה צריך

- ‏‎.NET SDK 8‏ ומעלה (הפרויקט מכוון ל-`net8.0`).
- **SQL Server LocalDB** – מותקן יחד עם Visual Studio. אפשר גם SQL Server Express או התקנה מלאה (ראו למטה).

### שלושה צעדים

```bash
# 1. מפתח החתימה של ה-JWT. זה סוד, ולכן הוא לא נמצא ב-repo – כל מי שמריצה מגדירה אחד משלה:
dotnet user-secrets set "Jwt:Key" "כאן-מחרוזת-אקראית-של-32-תווים-לפחות" --project WorkshopHub.API

# 2. הרצה
dotnet run --project WorkshopHub.API

# 3. Swagger
#    http://localhost:5080/swagger
```

זה הכול. **אין צורך ליצור את בסיס הנתונים ידנית:** בעלייה הראשונה (בסביבת Development) השרת מחיל את ה-migrations – מה שיוצר את בסיס הנתונים `WorkshopHubDb` – ומכניס נתוני דמו: משתמשים, 10 סדנאות, 25 מפגשים והרשמות. ההתנהגות נשלטת על ידי `Database:ApplyMigrationsOnStartup` ו-`Database:SeedDemoData` ב-`appsettings.Development.json`.

אם ה-`Jwt:Key` חסר, השרת לא עולה ומדפיס הודעה שמסבירה איך להגדיר אותו.

### שרת SQL אחר

ה-connection string של סביבת הפיתוח נמצא ב-`appsettings.Development.json` ומצביע על LocalDB עם Windows Authentication (ולכן אין בו סיסמה). כדי לעבוד מול שרת אחר, דורסים אותו ב-User Secrets – בלי לגעת בקבצים:

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost\SQLEXPRESS;Database=WorkshopHubDb;Trusted_Connection=True;TrustServerCertificate=True" --project WorkshopHub.API
```

### Migrations

ה-migrations כבר נמצאות ב-repo (`WorkshopHub.Data/Migrations`), והשרת מחיל אותן בעצמו. כלי ה-EF מוגדר כ-local tool:

```bash
dotnet tool restore
dotnet ef migrations add <Name>  -p WorkshopHub.Data -s WorkshopHub.API
dotnet ef database update        -p WorkshopHub.Data -s WorkshopHub.API
dotnet ef database drop --force  -p WorkshopHub.Data -s WorkshopHub.API    # איפוס: בהרצה הבאה הכול נוצר מחדש
```

## משתמשי דמו

נוצרים אוטומטית בסביבת הפיתוח (`WorkshopHub.Data/Seed`). אלה פרטי הדגמה בלבד; בבסיס הנתונים נשמר רק גיבוב הסיסמה.

| תפקיד | מייל | סיסמה |
|---|---|---|
| **Admin** (מנהלת) | `admin@workshophub.local` | `Admin#2026!` |
| **Member** (משתתפת) | `rachel@workshophub.local` | `Member#2026!` |
| **Member** | `sara@workshophub.local` | `Member#2026!` |
| **Member** | `chana@workshophub.local` | `Member#2026!` |
| **Member** | `leah@workshophub.local` | `Member#2026!` |
| **Member** | `miriam@workshophub.local` | `Member#2026!` |

נתוני הדמו כוללים מצבים מוכנים להדגמה: "אפיית חלות ולחמים" – נשאר **מקום אחרון**; "קליגרפיה עברית" – **מלא, עם רשימת המתנה**; "יסודות Excel למשרד" – מפגש עם **מקום יחיד**.

**התחברות ב-Swagger:** ‏`POST /api/tokens` עם מייל וסיסמה ← להעתיק את `accessToken` ← כפתור **Authorize** ← להדביק.

## ה-API

כל הנתיבים הם שמות משאבים ברבים, והפעולות נקבעות לפי ה-HTTP method. אין שמות פעולות ב-URL: הרשמה למערכת היא יצירת משתמש (`POST /api/users`), התחברות היא יצירת token‏ (`POST /api/tokens`), והרשמה למפגש היא יצירת הרשמה תחת המפגש.

| Method | נתיב | מי | הצלחה | שגיאות עיקריות |
|---|---|---|---|---|
| POST | `/api/users` | כולם | 201 + Location | 400, 409 |
| POST | `/api/tokens` | כולם | 200 | 400, 401 |
| GET | `/api/users/me` | מחוברת | 200 | 401 |
| GET | `/api/users/{id}` | עצמה / Admin | 200 | 401, 403, 404 |
| GET | `/api/users/me/registrations` | מחוברת | 200 (עמוד) | 401 |
| GET | `/api/categories`, `/api/categories/{id}` | כולם | 200 | 404 |
| POST / PUT / DELETE | `/api/categories`, `/api/categories/{id}` | Admin | 201 / 200 / 204 | 400, 401, 403, 404, 409 |
| GET | `/api/tags`, `/api/tags/{id}` | כולם | 200 | 404 |
| POST / DELETE | `/api/tags`, `/api/tags/{id}` | Admin | 201 / 204 | 400, 401, 403, 404, 409 |
| GET | `/api/workshops` | כולם | 200 (עמוד) | 400 |
| GET | `/api/workshops/{id}` | כולם | 200 | 404 |
| POST / PUT / DELETE | `/api/workshops`, `/api/workshops/{id}` | Admin | 201 / 200 / 204 | 400, 401, 403, 404, 409 |
| POST | `/api/workshops/{workshopId}/sessions` | Admin | 201 + Location | 400, 401, 403, 404 |
| GET | `/api/sessions` | כולם | 200 (עמוד) | 400 |
| GET | `/api/sessions/{id}` | כולם | 200 | 404 |
| PUT | `/api/sessions/{id}` | Admin | 200 | 400, 401, 403, 404, **409** |
| DELETE | `/api/sessions/{id}` | Admin | 204 | 401, 403, 404, 409 |
| **POST** | **`/api/sessions/{sessionId}/registrations`** | מחוברת | **201 + Location** | 401, 404, **409** |
| GET | `/api/sessions/{sessionId}/registrations` | Admin | 200 (עמוד) | 401, 403, 404 |
| GET | `/api/registrations/{id}` | בעלת ההרשמה / Admin | 200 | 401, 403, 404 |
| DELETE | `/api/registrations/{id}` | בעלת ההרשמה / Admin | 204 | 401, 403, 404, 409 |

**סינון, מיון ו-pagination** (לדוגמה): `GET /api/sessions?search=אפייה&categoryId=1&onlyAvailable=true&sortBy=SeatsLeft&descending=true&page=2&pageSize=5`

**מבנה שגיאה אחיד** – כל שגיאה, מכל מקור (Controller, ‏middleware, ולידציה, JWT), חוזרת כך:

```json
{
  "status": 409,
  "code": "concurrency_conflict",
  "message": "המקום נתפס על ידי משתתפת אחרת ממש ברגע זה. רענני את הרשימה ונסי שוב.",
  "correlationId": "5f0c1f0e7c0b4d7a9a1e2b3c4d5e6f70"
}
```

קודי ה-409: ‏`concurrency_conflict`, ‏`session_full`, ‏`already_registered`, ‏`already_cancelled`, ‏`session_already_started`, ‏`capacity_below_seats_taken`, ‏`has_active_registrations`, ‏`email_taken`, ‏`name_taken`, ‏`category_in_use`.

## בדיקות

```bash
dotnet test
```

63 בדיקות:

- **בדיקות יחידה** (`WorkshopHub.Tests/Unit`) – xUnit + Moq על שכבת ה-Service, עם repositories מדומים. `RegistrationServiceTests` בודק את הלוגיקה של המשאב המוגבל: המקרה התקין (המקום נתפס), והמקרים שנדחים (מפגש מלא, התנגשות עם בקשה אחרת, הרשמה כפולה).
- **בדיקות concurrency** (`WorkshopHub.Tests/Concurrency`) – מול SQL Server אמיתי. ה-fixture יוצר בסיס נתונים זמני בשם ייחודי ב-LocalDB, מחיל עליו את ה-migrations של הפרויקט, ומוחק אותו בסיום. לשרת אחר: משתנה הסביבה `WORKSHOPHUB_TEST_SQLSERVER` (connection string בלי שם בסיס נתונים).

להרצה של חלק מהבדיקות: `dotnet test --filter "Category!=Concurrency"` או `dotnet test --filter "Category=Concurrency"`.

## לוגים (NLog)

הקוד כותב לוגים דרך `ILogger` הרגיל של ‎.NET; ‏NLog מחובר מתחת (`builder.Host.UseNLog()`), וכל ההגדרות נמצאות ב-`WorkshopHub.API/nlog.config`. הקבצים נכתבים ל-`WorkshopHub.API/logs`:

- `workshophub-<תאריך>.log` – כל הלוגים.
- `warnings-<תאריך>.log` – ‏Warning ומעלה בלבד: התנגשויות על מקומות וכשלים.

כל בקשה מקבלת **CorrelationId** ב-`CorrelationIdMiddleware`. הוא מופיע בכל שורת לוג של הבקשה, חוזר ב-header‏ `X-Correlation-ID`, ומופיע בגוף של כל תשובת שגיאה – כך אפשר למצוא בלוג את הבקשה שהמשתמשת מדווחת עליה.

| רמה | מתי |
|---|---|
| Debug | פרטים לפיתוח (מצב המונים לפני הרשמה). פעיל רק ב-Development. |
| Information | זרימה רגילה: בקשה נכנסה / הסתיימה, משתמשת נרשמה, מפגש נוצר. |
| Warning | חריג שטופל: **התנגשות על מקום**, ניסיון לבטל הרשמה של אחרת, התחברות שנכשלה. |
| Error | כשל: חריגה שלא טופלה (נתפסת ב-`ExceptionHandlingMiddleware`). |

**מה לא נכתב ללוג:** סיסמאות, tokens, גוף הבקשות ותוכן בקשת ההרשמה למערכת. ה-`RequestLoggingMiddleware` כותב רק method, נתיב, סטטוס ומשך.

## קונפיגורציה וסודות

| מקור | מה יש בו |
|---|---|
| `appsettings.json` | ערכים שאינם סודיים ומשותפים לכל הסביבות (Issuer, Audience ותוקף ה-JWT) |
| `appsettings.Development.json` | הגדרות סביבת הפיתוח: connection string ל-LocalDB (Windows Authentication, בלי סיסמה), CORS ללקוח, לוג ברמת Debug, אתחול אוטומטי של בסיס הנתונים |
| **User Secrets** | `Jwt:Key` (חובה), ובמידת הצורך `ConnectionStrings:Default` |

**אין אף סוד בקוד או ב-repo.** ההגדרות נקראות דרך `IOptions<JwtSettings>` ו-`IOptions<DatabaseSettings>`, עם ולידציה בעליית השרת (`ValidateDataAnnotations().ValidateOnStart()`).

הערה: AutoMapper (מגרסה 15) כותב אזהרה ללוג בעליית השרת כשלא הוגדר מפתח רישיון. הספרייה עובדת כרגיל גם בלעדיו. מי שיש לה מפתח יכולה להגדיר אותו: `dotnet user-secrets set "AutoMapper:LicenseKey" "<key>" --project WorkshopHub.API`.

## היכן כל דרישה ממומשת

| # | דרישה | איפה |
|---|---|---|
| 1 | Web API & REST, סטטוסים, Swagger | `API/Controllers`, ‏`ApiControllerBase.ToErrorResult`; ‏201 עם Location דרך `CreatedAtAction`; ‏Swagger עם כפתור Authorize ב-`Program.cs` |
| 2 | Model Binding & Validation | `[FromRoute]` / `[FromQuery]` / `[FromBody]` בכל ה-actions; ‏Data Annotations ב-`Core/DTOs`; בדיקת ה-ModelState ב-`ErrorResponseFactory.FromInvalidModelState`; ולידציה עסקית ב-Services |
| 3 | ארכיטקטורה בשכבות | ארבעה פרויקטים; קובצי ה-`.csproj` מראים את כיוון התלויות |
| 4 | Dependency Injection | הזרקה דרך constructors בלבד; הרישום וההסבר על ה-lifetimes ב-`Program.cs`; ‏`ValidateScopes` + `ValidateOnBuild` |
| 5 | DbContext ו-Migrations | `Data/AppDbContext.cs`, ‏`Data/Migrations` (שתי migrations), ‏`Data/Seed/DbInitializer.cs` |
| 6 | שאילתות ו-CRUD | `Data/Repositories`; ‏pagination ב-`Extensions/QueryableExtensions.cs` (Skip/Take בתוך השאילתה); ‏`AsNoTracking` בכל קריאה בלבד |
| 7 | קשרי גומלין | Fluent API ב-`Data/Configurations`; ‏`Include` / `ThenInclude`; מניעת N+1 ב-`SessionService.AttachMyRegistrationsAsync` |
| 8 | DTO & AutoMapper | `Core/DTOs` (Request ו-Response נפרדים), ‏`Service/Mapping/MappingProfile.cs` |
| 9 | Async | כל השרשרת אסינכרונית, ו-`CancellationToken` עובר מה-Controller עד ה-DbContext |
| 10 | Middleware | `API/Middleware` – טיפול גלובלי בשגיאות, CorrelationId, לוג בקשות; הסדר ב-`Program.cs` |
| 11 | Configuration | `appsettings.json`, ‏`appsettings.Development.json`, ‏`IOptions`, ‏User Secrets |
| 12 | JWT | `Service/Security/JwtTokenService.cs`, ‏`API/Security/ConfigureJwtBearerOptions.cs`; שני תפקידים; `[Authorize]` ו-`[Authorize(Roles = "Admin")]`; הרשאות ברמת המשאב ב-Services |
| 13 | Unit Tests | `Tests/Unit` (xUnit + Moq), ‏`Tests/Concurrency` |
| ג | תחרות על משאב | ראו "המשאב המוגבל והתחרות עליו" למעלה |
| ד | NLog | `API/nlog.config`, ‏`API/Middleware`, ורמות הלוג ב-Services |

</div>
