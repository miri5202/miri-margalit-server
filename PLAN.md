<div dir="rtl">

# WorkshopHub – תוכנית הפרויקט

> **מה יש במסמך הזה?** הסבר מסודר של המערכת: מה היא עושה, איך היא בנויה, ולמה כל החלטה התקבלה.
> בסוף המסמך יש **טבלת בדיקה מול כל דרישה במסמך ההנחיות**, ו**שאלות שכדאי לדעת לענות עליהן** בהגנה על הפרויקט.
>
> **מצב:** כל השלבים הושלמו ✅. הקוד קומפל, 63 הבדיקות עברו, והמערכת נבדקה מקצה לקצה על המחשב המקומי.

---

## תוכן עניינים

1. [המערכת בקצרה](#1)
2. [המשאב המוגבל – הלב של הפרויקט](#2)
3. [משתמשים והרשאות](#3)
4. [ארכיטקטורה – 4 שכבות](#4)
5. [ישויות וקשרים](#5)
6. [הטיפול בתחרות – איך זה עובד בפועל](#6)
7. [רשימת ה-Endpoints](#7)
8. [שגיאות – מבנה אחיד](#8)
9. [הגדרות וסודות](#9)
10. [לוגים עם NLog](#10)
11. [בדיקות](#11)
12. [לקוח React](#12)
13. [שלבי העבודה](#13)
14. [בדיקה מול ההנחיות – צ'קליסט סופי](#14)
15. [שאלות שכדאי לדעת לענות עליהן](#15)
16. [מה נשאר לעשות לפני ההגשה](#16)
17. [יומן החלטות](#17)

---

<a id="1"></a>
## 1. המערכת בקצרה

**WorkshopHub** היא מערכת הרשמה לסדנאות.

- **סדנה** (למשל "אפיית חלות", "יסודות Excel") מתקיימת בכמה **מפגשים**.
- לכל מפגש יש **מספר מקומות מוגבל**.
- משתתפת נרשמת למפגש. אם הוא מלא, היא יכולה להצטרף ל**רשימת המתנה**.
- כשמישהי מבטלת, **הממתינה הוותיקה ביותר מקבלת את המקום אוטומטית**.
- מנהלת מנהלת סדנאות, מפגשים, קטגוריות ותגיות, ורואה את כל ההרשמות.

| | |
|---|---|
| **טכנולוגיה** | ASP.NET Core Web API, ‏.NET 8 |
| **בסיס נתונים** | SQL Server (LocalDB), ‏EF Core 8, Code-First |
| **לקוח** | React + Vite (repository נפרד) |
| **ריצה** | מקומית בלבד, בלי ענן |

---

<a id="2"></a>
## 2. המשאב המוגבל – הלב של הפרויקט

### מה המשאב?
**מקום במפגש.** למפגש "יסודות Excel" יש, למשל, מקום אחד בלבד.

### איפה התחרות?
בפעולת ההרשמה: `POST /api/sessions/{sessionId}/registrations`.

### מבחן הרעיון מתוך ההנחיות
> "שתי משתמשות פעלו בו-זמנית, שתיהן קיבלו אישור, ואסור היה ששתיהן יקבלו."

נשאר מקום אחרון. רחל ולאה לוחצות "הרשמה" באותה מילישנייה. שתיהן קוראות את המפגש ורואות **"נשאר מקום 1"**, ושתיהן עוברות את הבדיקה. בלי הגנה, שתיהן היו מקבלות אישור על אותו מקום. ✅ **הרעיון עומד בתנאי.**

### איך המערכת מגיבה?
| מי | מה קורה | מה היא מקבלת |
|---|---|---|
| הראשונה ששומרת | המקום שלה | `201 Created` |
| השנייה | השמירה שלה נדחית, ושום דבר ממנה לא נשמר | `409 Conflict` עם ההודעה: *"המקום נתפס על ידי משתתפת אחרת ממש ברגע זה. רענני את הרשימה ונסי שוב."* |

בנוסף נכתבת **שורת Warning בלוג** עם מזהה הבקשה.

> **הוכחה מהבדיקה שהרצנו:** 8 משתתפות שלחו הרשמה באותו רגע למפגש עם מקום אחד. תוצאה: 201 אחד ושבעה 409. המפגש נשאר 1/1.
> 12 משתתפות על 3 מקומות: שלושה 201 ותשעה 409. המפגש נשאר 3/3.

---

<a id="3"></a>
## 3. משתמשים והרשאות

שני תפקידים (Roles): **Member** (משתתפת) ו-**Admin** (מנהלת).

| פעולה | אורחת | Member | Admin |
|---|:-:|:-:|:-:|
| צפייה בסדנאות, מפגשים, קטגוריות ותגיות | ✅ | ✅ | ✅ |
| הרשמה למערכת | ✅ | – | – |
| הרשמה למפגש / הצטרפות להמתנה | ❌ | ✅ | ✅ |
| ביטול הרשמה | ❌ | ✅ רק שלה | ✅ של כל אחת |
| צפייה בהרשמות שלי | ❌ | ✅ | ✅ |
| צפייה בכל ההרשמות למפגש | ❌ | ❌ | ✅ |
| יצירה / עדכון / מחיקה של סדנאות, מפגשים, קטגוריות ותגיות | ❌ | ❌ | ✅ |

- הרשמה למערכת **תמיד** יוצרת Member. אי אפשר להירשם כמנהלת דרך ה-API.
- ההרשאות נבדקות **בשרת**, בשתי רמות:
  - **לפי תפקיד:** `[Authorize(Roles = "Admin")]` על ה-Controller.
  - **לפי בעלות:** "האם ההרשמה הזו שלך?" נבדק בשכבת ה-Service.
- **הרשאה שנבדקת רק בצד הלקוח אינה הרשאה.** הלקוח מסתיר כפתורים רק לנוחות.

---

<a id="4"></a>
## 4. ארכיטקטורה – 4 שכבות

```
WorkshopHub.sln
├── WorkshopHub.Core      ישויות, Enums, DTOs, ממשקים, Result, קבועי שגיאות
├── WorkshopHub.Data      DbContext, הגדרות Fluent API, Repositories, Migrations, Seed
├── WorkshopHub.Service   לוגיקה עסקית, AutoMapper Profile, JWT, גיבוב סיסמאות
├── WorkshopHub.API       Controllers, Middleware, Program.cs, nlog.config
└── WorkshopHub.Tests     xUnit + Moq, ובדיקות concurrency מול SQL Server אמיתי
```

### כיוון התלויות
```
Core     →  כלום (אין אף חבילה חיצונית)
Data     →  Core
Service  →  Core
API      →  Service, Core   (+ Data רק לרישום השירותים ב-Program.cs)
```

**שתי הטעויות שההנחיות מזהירות מהן – ואיך נמנענו:**
| טעות | אצלנו |
|---|---|
| Core מפנה ל-EF Core | ✅ בקובץ `WorkshopHub.Core.csproj` אין אף `PackageReference` |
| Controller שמזריק `DbContext` | ✅ ה-Controllers מזריקים רק Services. `AppDbContext` מופיע בשכבת ה-API רק ב-`Program.cs`, לרישום |

### חבילות NuGet
| פרויקט | חבילות |
|---|---|
| Core | אין |
| Data | `Microsoft.EntityFrameworkCore.SqlServer` 8.0.31 |
| Service | `AutoMapper` 16.2.0, ‏`System.IdentityModel.Tokens.Jwt` |
| API | `Microsoft.AspNetCore.Authentication.JwtBearer`, ‏`Swashbuckle.AspNetCore`, ‏`Microsoft.EntityFrameworkCore.Design`, ‏`NLog.Web.AspNetCore` |
| Tests | `xunit`, ‏`Moq`, ‏`Microsoft.NET.Test.Sdk` |

### למה ה-Service לא מכיר את EF Core?
כי Service מפנה רק ל-Core. הוא עובד מול **ממשקים** (`ISessionRepository`, `IUnitOfWork`) שמוגדרים ב-Core, והמימוש שלהם יושב ב-Data.
לכן שכבת ה-Data **מתרגמת** את החריגה של EF Core לחריגה של Core (פירוט בסעיף 6).

---

<a id="5"></a>
## 5. ישויות וקשרים

### 6 ישויות
| ישות | מה היא | שדות חשובים |
|---|---|---|
| **User** | משתמשת | `Email` (ייחודי), `PasswordHash`, `Role` |
| **Category** | קטגוריה של סדנאות | `Name` (ייחודי) |
| **Tag** | תגית ("למתחילות", "ערב") | `Name` (ייחודי) |
| **Workshop** | סדנה | `Title`, `InstructorName`, `CategoryId` |
| **Session** | מפגש – **המשאב המוגבל** | `Capacity`, `SeatsTaken`, `WaitlistCount`, **`RowVersion`** |
| **Registration** | הרשמה | `Status`: ‏Confirmed / Waitlisted / Cancelled |

### הקשרים (מוגדרים ב-Fluent API, בתיקייה `Data/Configurations`)
```
Category  1 ───* Workshop        one-to-many
Workshop  1 ───* Session         one-to-many
Session   1 ───* Registration    one-to-many
User      1 ───* Registration    one-to-many
Workshop  * ───* Tag             many-to-many  (טבלת קישור WorkshopTags)
```

### הגנות ברמת בסיס הנתונים
גם אם יהיה באג בקוד, בסיס הנתונים עצמו לא יסכים לשמור מצב לא תקין:
| הגנה | מה היא מונעת |
|---|---|
| `CHECK`: ‏`SeatsTaken` בין 0 ל-`Capacity` | יותר נרשמות ממקומות |
| `CHECK`: ‏`Capacity > 0`, ‏`WaitlistCount >= 0` | ערכים לא הגיוניים |
| אינדקס ייחודי מסונן על `(SessionId, UserId)` להרשמות שלא בוטלו | אותה משתמשת רשומה פעמיים לאותו מפגש |
| אינדקס ייחודי על `Email` | שני חשבונות עם אותו מייל |

---

<a id="6"></a>
## 6. הטיפול בתחרות – איך זה עובד בפועל

### הבעיה במשפט אחד
בין הרגע שקראנו את המפגש ("יש מקום") לבין הרגע ששמרנו ("תפסתי"), מישהי אחרת יכלה לשנות אותו.

### שתי גישות אפשריות
| גישה | איך | יתרון | חיסרון |
|---|---|---|---|
| Pessimistic | נועלים את השורה לפני הקריאה | בטוח | המתנות, סכנת deadlock |
| **Optimistic** ✅ | לא נועלים. בודקים בזמן השמירה אם השורה השתנתה | מהיר, מתאים ל-web | צריך לטפל בהתנגשות |

ההנחיות דורשות **Optimistic**, וזה מה שבנינו.

### ה-concurrency token
```csharp
// WorkshopHub.Core/Entities/Session.cs
[Timestamp]
public byte[] RowVersion { get; set; }
```
```csharp
// WorkshopHub.Data/Configurations/SessionConfiguration.cs
builder.Property(s => s.RowVersion).IsRowVersion();
```
- ב-SQL Server העמודה מסוג `rowversion`, ו**SQL Server מעדכן אותה אוטומטית** בכל שינוי בשורה.
- `[Timestamp]` מגיע מ-`System.ComponentModel.DataAnnotations`, שהוא חלק מ-.NET עצמו, ולכן Core עדיין בלי חבילות חיצוניות.

### מה EF Core עושה עם ה-token
כשרחל שומרת, EF Core שולח:
```sql
UPDATE Sessions SET SeatsTaken = 1
WHERE Id = 10 AND RowVersion = <הערך שרחל קראה>
```
- **רחל ראשונה:** השורה נמצאת ומתעדכנת, וה-RowVersion משתנה.
- **לאה שנייה:** ה-RowVersion שלאה קראה כבר לא קיים, **אף שורה לא מתעדכנת**, ו-EF Core זורק `DbUpdateConcurrencyException`.

### השלבים בתוך `RegistrationService.RegisterAsync`
| שלב | מה קורה |
|---|---|
| 1. קריאה | טוענים את המפגש **עם מעקב**, כך ש-EF זוכר את ה-RowVersion שנקרא |
| 2. בדיקות | המפגש קיים? לא התחיל? המשתמשת לא רשומה כבר? |
| 3. שינוי | יש מקום → `SeatsTaken++`, הרשמה Confirmed. מלא + ביקשה המתנה → `WaitlistCount++`, הרשמה Waitlisted. מלא בלי המתנה → 409 `session_full` |
| 4. שמירה | `SaveChangesAsync` אחד: ה-UPDATE של המפגש וה-INSERT של ההרשמה **באותה טרנזקציה**. או ששניהם נשמרים, או שאף אחד |

> **"הבדיקה העסקית והשמירה חייבות להיות באותה טרנזקציה"** – הבדיקה שבאמת קובעת היא תנאי ה-`WHERE RowVersion = ...`, והיא רצה בתוך הטרנזקציה של השמירה. הבדיקה בשלב 2 רק מאפשרת להחזיר הודעה מדויקת במקרה הרגיל. זה בדיוק הדפוס שמופיע בדוגמה שבמסמך ההנחיות.

### המסלול של ההתנגשות בין השכבות
| שכבה | מה קורה | קובץ |
|---|---|---|
| **Data** | `catch (DbUpdateConcurrencyException)` – חריגה **ספציפית**, לא `Exception` כללי – ומתרגמת ל-`ConcurrencyConflictException` של Core | `Repositories/UnitOfWork.cs` |
| **Service** | תופסת `ConcurrencyConflictException`, כותבת **Warning** ללוג, ומחזירה `Result` מסוג Conflict | `Services/RegistrationService.cs` |
| **API** | מתרגמת Conflict ל-**409** במבנה השגיאה האחיד | `Controllers/ApiControllerBase.cs` |
| **רשת ביטחון** | אם פעולה חדשה תשכח לתפוס את החריגה, ה-Middleware עדיין מחזיר 409 ולא 500 | `Middleware/ExceptionHandlingMiddleware.cs` |

### שלוש החלטות שמחזקות את ההגנה

**1. כל פעולה על הרשמות משנה את שורת המפגש.**
גם הצטרפות לרשימת ההמתנה מעדכנת את `WaitlistCount`. כך כל שתי פעולות מקבילות על אותו מפגש מתנגשות על אותו token: הרשמה מול הרשמה, הרשמה מול ביטול, וביטול מול הצטרפות להמתנה.
*בלי המונה הזה:* משתתפת יכלה להיכנס להמתנה בדיוק ברגע שמקום התפנה, ולהישאר ממתינה למרות שיש מקום.

**2. ביטול מקדם ממתינה באותה שמירה.**
כשמבטלים הרשמה מאושרת, הממתינה הוותיקה עוברת ל-Confirmed **באותו `SaveChanges`**. המקום לא "נעלם" ולא מוכפל.

**3. התחרות קיימת גם בעריכה של מנהלת.**
ב-`PUT /api/sessions/{id}` הלקוח **מחזיר את ה-`rowVersion` שקיבל** כשטען את המפגש. אם מישהי נרשמה מאז, העדכון נדחה ב-409 במקום לדרוס את השינוי שלה.

### קודי ה-409 במערכת
| קוד | מתי |
|---|---|
| `concurrency_conflict` | מישהי אחרת שינתה את המפגש בין הקריאה לשמירה |
| `session_full` | המפגש מלא (וביקשו בלי המתנה) |
| `already_registered` | המשתמשת כבר רשומה או ממתינה |
| `already_cancelled` | ביטול של הרשמה שכבר בוטלה |
| `session_already_started` | הרשמה או ביטול למפגש שהתחיל |
| `capacity_below_seats_taken` | הקטנת מקומות מתחת למספר הנרשמות |
| `has_active_registrations` | מחיקת מפגש או סדנה שיש בהם הרשמות |
| `email_taken`, ‏`name_taken`, ‏`category_in_use` | כפילויות ומחיקות לא חוקיות |

> **למה 409 ולא 400 או 500?** הבקשה עצמה תקינה (לכן לא 400), והשרת לא קרס (לכן לא 500). היא פשוט **מתנגשת עם המצב הנוכחי** של המשאב – וזו בדיוק ההגדרה של 409.

---

<a id="7"></a>
## 7. רשימת ה-Endpoints

לפי REST: שמות משאבים **ברבים**, והפעולה נקבעת לפי ה-HTTP Method. **אין שמות פעולות ב-URL**, ולכן:
- הרשמה למערכת = **יצירת משתמש**: `POST /api/users`
- התחברות = **יצירת token**: `POST /api/tokens`
- הרשמה למפגש = **יצירת הרשמה תחת המפגש**: `POST /api/sessions/{id}/registrations`

| Method | נתיב | מי | הצלחה | שגיאות |
|---|---|---|---|---|
| POST | `/api/users` | כולם | 201 + Location | 400, 409 |
| POST | `/api/tokens` | כולם | 200 | 400, 401 |
| GET | `/api/users/me` | מחוברת | 200 | 401 |
| GET | `/api/users/{id}` | עצמה / Admin | 200 | 401, 403, 404 |
| GET | `/api/users/me/registrations` | מחוברת | 200 (עמוד) | 400, 401 |
| GET | `/api/categories` + `/{id}` | כולם | 200 | 404 |
| POST / PUT / DELETE | `/api/categories` + `/{id}` | Admin | 201 / 200 / 204 | 400, 401, 403, 404, 409 |
| GET | `/api/tags` + `/{id}` | כולם | 200 | 404 |
| POST / DELETE | `/api/tags` + `/{id}` | Admin | 201 / 204 | 400, 401, 403, 404, 409 |
| GET | `/api/workshops` | כולם | 200 (עמוד) | 400 |
| GET | `/api/workshops/{id}` | כולם | 200 | 404 |
| POST / PUT / DELETE | `/api/workshops` + `/{id}` | Admin | 201 / 200 / 204 | 400, 401, 403, 404, 409 |
| POST | `/api/workshops/{id}/sessions` | Admin | 201 + Location | 400, 401, 403, 404 |
| GET | `/api/sessions` | כולם | 200 (עמוד) | 400 |
| GET | `/api/sessions/{id}` | כולם | 200 | 404 |
| PUT | `/api/sessions/{id}` | Admin | 200 | 400, 401, 403, 404, **409** |
| DELETE | `/api/sessions/{id}` | Admin | 204 | 401, 403, 404, 409 |
| **POST** | **`/api/sessions/{id}/registrations`** | **מחוברת** | **201 + Location** | **401, 404, 409** |
| GET | `/api/sessions/{id}/registrations` | Admin | 200 (עמוד) | 401, 403, 404 |
| GET | `/api/registrations/{id}` | בעלת ההרשמה / Admin | 200 | 401, 403, 404 |
| DELETE | `/api/registrations/{id}` | בעלת ההרשמה / Admin | 204 | 401, 403, 404, 409 |

**סה"כ 28 endpoints**, כולם מתועדים ב-Swagger (`http://localhost:5080/swagger`), כולל כפתור **Authorize** ל-JWT.

### Model Binding
| מקור | שימוש | דוגמה |
|---|---|---|
| `[FromRoute]` | מזהים בנתיב | `{id}`, ‏`{sessionId}` |
| `[FromQuery]` | סינון, מיון ועמודים | `?search=Excel&onlyAvailable=true&page=2&pageSize=5` |
| `[FromBody]` | יצירה ועדכון | `RegisterUserRequest`, ‏`SessionUpdateRequest` |

### סינון, מיון ו-Pagination
- **מפגשים:** חיפוש, סדנה, קטגוריה, טווח תאריכים, רק עם מקום פנוי, כולל עבר. מיון לפי מועד / מקומות פנויים / שם סדנה.
- **סדנאות:** חיפוש, קטגוריה, תגית. מיון לפי שם / מנחה / קטגוריה.
- **Pagination אמיתי:** `Skip` ו-`Take` **בתוך השאילתה** (מתורגם ל-`OFFSET ... FETCH` ב-SQL), ולא `ToList` ואז חיתוך בזיכרון.
- התשובה כוללת: `items`, ‏`page`, ‏`pageSize`, ‏`totalCount`, ‏`totalPages`, ‏`hasNext`, ‏`hasPrevious`.
- `pageSize` מוגבל ל-1 עד 100.

---

<a id="8"></a>
## 8. שגיאות – מבנה אחיד

**כל** שגיאה במערכת – מ-Controller, מה-Middleware, מוולידציה או מה-JWT – חוזרת באותו מבנה:

```json
{
  "status": 409,
  "code": "concurrency_conflict",
  "message": "המקום נתפס על ידי משתתפת אחרת ממש ברגע זה. רענני את הרשימה ונסי שוב.",
  "correlationId": "5f0c1f0e7c0b4d7a9a1e2b3c4d5e6f70"
}
```
בשגיאת ולידציה (400) מתווסף גם `errors` – פירוט לפי שדה.

### איפה כל סוג ולידציה
| סוג | איפה | דוגמה |
|---|---|---|
| **מבנית** – לא תלויה בנתונים | Data Annotations על ה-DTO, נבדקת אוטומטית דרך ה-ModelState | `[Required]`, ‏`[EmailAddress]`, ‏`[Range(1, 500)]` |
| **עסקית** – תלויה במצב הנתונים | **שכבת ה-Service בלבד** | המפגש מלא, המייל כבר קיים, המפגש בעבר, הקטגוריה לא קיימת |

### Result במקום חריגות
מצבים **צפויים** (לא נמצא, אין הרשאה, התנגשות) לא נזרקים כחריגה. ה-Service מחזיר `Result` עם סוג שגיאה, וה-Controller מתרגם אותו לסטטוס:

| סוג ב-Result | סטטוס |
|---|---|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |

חריגות נשמרות למצבים **שבאמת לא צפויים**. אלה נתפסים ב-Middleware וחוזרים כ-500, בלי stack trace ובלי פרטים פנימיים.

---

<a id="9"></a>
## 9. הגדרות וסודות

| מקור | מה יש בו | נכנס ל-repo? |
|---|---|---|
| `appsettings.json` | Issuer, ‏Audience ותוקף ה-JWT, רמת לוג Info | ✅ |
| `appsettings.Development.json` | connection string ל-LocalDB (**בלי סיסמה**, Windows Authentication), CORS ללקוח, רמת לוג Debug, אתחול אוטומטי של בסיס הנתונים | ✅ |
| **User Secrets** | **`Jwt:Key`** – מפתח החתימה | ❌ נשמר מחוץ לתיקיית הפרויקט |

- הגישה להגדרות דרך **`IOptions<JwtSettings>`** ו-**`IOptions<DatabaseSettings>`**.
- ההגדרות נבדקות **בעליית השרת** (`ValidateDataAnnotations().ValidateOnStart()`). אם `Jwt:Key` חסר או קצר מ-32 תווים, השרת לא עולה ומדפיס הודעה שמסבירה מה לעשות.
- **אין סוד בקוד ואין סוד ב-repo:** אין סיסמת בסיס נתונים (LocalDB עובד עם Windows Authentication), ומפתח ה-JWT נמצא רק ב-User Secrets.

### Dependency Injection
| Lifetime | מה | למה |
|---|---|---|
| **Scoped** | `AppDbContext`, כל ה-Repositories, ה-UnitOfWork וה-Services | מופע אחד **לכל בקשת HTTP**. כל ה-Repositories של הבקשה חולקים את אותו DbContext, ולכן `SaveChanges` אחד שומר הכול בטרנזקציה אחת |
| **Singleton** | `PasswordHasher`, ‏`TokenService`, ‏`TimeProvider` | אין להם מצב, ואין להם תלות ב-Scoped |

> **למה Singleton שמחזיק Scoped הוא באג?** ה-Singleton נוצר פעם אחת, ולכן הוא "תופס" את ה-DbContext של הבקשה הראשונה ומשתמש בו לנצח. ה-DbContext אינו thread-safe, ומצבו מצטבר ומתיישן.
> כדי לתפוס טעות כזו מוקדם, הפעלנו `ValidateScopes = true` ו-`ValidateOnBuild = true`: השרת לא יעלה בכלל אם יש תלות כזו.

### סדר ה-Pipeline
```
1. ExceptionHandling      ← ראשון, כדי לעטוף את כל השאר
2. CorrelationId          ← לפני כל middleware שכותב לוגים
3. RequestLogging         ← לוג לכל בקשה
4. Swagger (רק בפיתוח)
5. CORS
6. Authentication         ← "מי את?"
7. Authorization          ← "מה מותר לך?"
8. Controllers
```

---

<a id="10"></a>
## 10. לוגים עם NLog

**העיקרון:** הקוד בכל השכבות כותב דרך **`ILogger<T>` הרגיל של .NET**. NLog מחובר **מתחת**, ב-`Program.cs` (`builder.Host.UseNLog()`).
כך אפשר להחליף ספריית לוגים בלי לגעת ב-Service או ב-Data – אותו רעיון של הפרדת תלויות כמו בארכיטקטורה.

- **החבילה:** `NLog.Web.AspNetCore`, רק בפרויקט ה-API.
- **ההגדרות:** בקובץ `WorkshopHub.API/nlog.config`.
- **הקבצים:** נכתבים לתיקייה `WorkshopHub.API/logs`:
  - `workshophub-<תאריך>.log` – כל הלוגים.
  - `warnings-<תאריך>.log` – רק Warning ומעלה: התנגשויות וכשלים.

### CorrelationId
כל בקשה מקבלת מזהה ייחודי ב-`CorrelationIdMiddleware`. המזהה:
- מופיע **בכל שורת לוג** של הבקשה, מכל שכבה;
- חוזר ללקוח ב-header‏ `X-Correlation-ID`;
- מופיע בגוף של כל תשובת שגיאה.

כך משתמשת שמדווחת על תקלה יכולה למסור את המזהה, ובלוג מוצאים את כל מה שקרה בבקשה שלה.

### מה נרשם ובאיזו רמה
| רמה | מתי | דוגמה מהלוג האמיתי |
|---|---|---|
| **Debug** | פרטים לפיתוח (רק בסביבת Development) | `Session 26 has 0/1 seats taken and 0 waiting before user 10 registers` |
| **Information** | זרימה רגילה | `HTTP POST /api/tokens responded 200 in 61 ms`, ‏`New user 7 registered` |
| **Warning** | חריג שטופל | `Concurrency conflict: user 9 lost the race for session 26`, התחברות שנכשלה |
| **Error** | כשל – חריגה לא צפויה | נתפסת ב-`ExceptionHandlingMiddleware` |

### מה **לא** נכתב ללוג
❌ סיסמאות ❌ tokens ❌ גוף הבקשות ❌ תוכן בקשת ההרשמה למערכת ❌ כתובות מייל

ה-`RequestLoggingMiddleware` כותב רק Method, נתיב, סטטוס ומשך.

> **נבדק בפועל:** אחרי הרשמות, התחברויות (כולל סיסמה שגויה), 409 ו-race demo, חיפשנו בקבצי הלוג סיסמאות, tokens וכתובות מייל – **0 תוצאות**.

---

<a id="11"></a>
## 11. בדיקות

**63 בדיקות, כולן עוברות.** ‏(`dotnet test`)

### בדיקות יחידה – xUnit + Moq (55)
על שכבת ה-Service, עם Repositories **מדומים**.

| קובץ | מה נבדק | מס' |
|---|---|---|
| `RegistrationServiceTests` | **הלוגיקה של המשאב המוגבל** – ראו פירוט למטה | 16 |
| `SessionServiceTests` | יצירת מפגש, עדכון עם RowVersion, קידום ממתינות, מחיקה, מניעת N+1 | 11 |
| `AuthServiceTests` | הרשמה, מייל קיים, התחברות, סיסמה שגויה | 6 |
| `WorkshopServiceTests` | יצירה ועדכון עם תגיות (many-to-many), קטגוריה לא קיימת | 6 |
| `MappingProfileTests` | תקינות כל מיפויי AutoMapper, ושה-PasswordHash לא יוצא מה-API | 6 |
| `SecurityTests` | גיבוב סיסמאות, יצירת JWT ובדיקת החתימה שלו | 10 |

**המשאב המוגבל – גם המקרה התקין וגם המקרים שנדחים (דרישת חובה):**
| מקרה | תוצאה צפויה |
|---|---|
| ✅ יש מקום | ההרשמה Confirmed, ‏`SeatsTaken` עולה, שמירה אחת |
| ❌ המפגש מלא | 409 `session_full`, שום דבר לא נשמר |
| ✅ מלא + ביקשה המתנה | Waitlisted, ‏`SeatsTaken` לא משתנה |
| ❌ **מישהי תפסה את המקום בין הקריאה לשמירה** | 409 `concurrency_conflict` – לא חריגה ולא 500 |
| ❌ כבר רשומה | 409 `already_registered` |
| ❌ המפגש כבר התחיל | 409 |
| ✅ ביטול כשיש ממתינה | הממתינה מקודמת, והמפגש נשאר מלא |
| ❌ ביטול הרשמה של מישהי אחרת | 403 |

### בדיקות Concurrency – מול SQL Server אמיתי (8)
**למה לא עם Moq?** ‏`rowversion` הוא מנגנון של SQL Server עצמו. בדיקה מול נתונים מדומים או בסיס נתונים בזיכרון **לא הייתה מוכיחה כלום**.
לכן כל הרצה יוצרת בסיס נתונים זמני ב-LocalDB, מחילה עליו את ה-migrations האמיתיות של הפרויקט, ומוחקת אותו בסוף.

| בדיקה | מה היא מוכיחה |
|---|---|
| ⭐ **`TwoDbContexts_ChangeTheSameSession_FirstSaveSucceeds_SecondThrowsDbUpdateConcurrencyException`** | **הבדיקה שנדרשת במדריך:** שני `DbContext` נפרדים קוראים את אותו מפגש, שניהם משנים אותו, הראשון שומר בהצלחה והשני מקבל `DbUpdateConcurrencyException`. בבסיס הנתונים נשאר מקום תפוס אחד – לא שניים |
| `UnitOfWork_TranslatesTheEfExceptionIntoConcurrencyConflictException` | שכבת ה-Data מתרגמת נכון את החריגה |
| `TwoUsers_RegisterForTheLastSeatAtTheSameMoment_...` | אותו תרחיש דרך ה-Service: בדיוק אחת מאושרת, השנייה מקבלת Conflict, ולא נשארת הרשמה "יתומה" |
| `ManyUsers_RaceForAFewSeats_TheSessionIsNeverOversold` | ‏12 משתתפות על 3 מקומות – אף פעם לא מעל הקיבולת |
| `UpdatingASession_WithTheRowVersionLoadedBefore...` | עריכה עם RowVersion ישן נדחית, ועם RowVersion עדכני מצליחה |
| `CancellingAConfirmedRegistration_PromotesTheOldestWaitlistedUser` | הקידום מרשימת ההמתנה עובד מול בסיס נתונים אמיתי |
| `Database_RejectsASecondActiveRegistration...` | האינדקס הייחודי עוצר הרשמה כפולה גם אם עוקפים את הקוד |
| `Database_RejectsMoreSeatsTakenThanCapacity...` | ה-CHECK constraint עוצר יותר נרשמות ממקומות |

### סקריפט הדגמה חי
```
node scripts/race-demo.mjs --users 8 --capacity 1
```
יוצר מפגש עם מקום אחד, שולח 8 בקשות הרשמה באותו רגע ומדפיס מה כל אחת קיבלה.

---

<a id="12"></a>
## 12. לקוח React

ההנחיות: הלקוח **לא נבדק**, הוא רק הוכחה שהשרת עובד. לכן הוא מינימלי.

| # | מסך | קובץ |
|---|---|---|
| 1 | התחברות והרשמה. ה-JWT נשמר ונשלח ב-header‏ `Authorization: Bearer` | `src/pages/AuthPage.jsx`, ‏`src/api.js` |
| 2 | רשימת מפגשים עם **pagination אמיתי מול השרת** וסינון | `src/pages/SessionsPage.jsx` |
| 3 | כפתור "הרשמה" / "הצטרפות לרשימת ההמתנה" / "ביטול" – הפעולה על המשאב המוגבל | `src/pages/SessionsPage.jsx`, ‏`MyRegistrationsPage.jsx` |
| 4 | ⭐ **הצגה ברורה של 409** – הודעה כתומה בולטת, הרשימה נטענת מחדש עם המצב האמיתי, והצעה לצעד הבא (להצטרף להמתנה או לנסות שוב) | `src/components/ErrorNotice.jsx` |

**איך רואים 409 בעיניים:** פותחים את הלקוח בשני חלונות (אחד בגלישה בסתר), מתחברים כשתי משתתפות שונות, ולוחצים "הרשמה" על מפגש עם מקום אחרון – קודם בחלון אחד, ואז בשני **בלי לרענן**.

---

<a id="13"></a>
## 13. שלבי העבודה

| # | שלב | מה נעשה | סטטוס |
|---|---|---|---|
| 0 | בחירת רעיון | הרשמה לסדנאות. בדיקת "מבחן הרעיון" | ✅ |
| 1 | Solution | 5 פרויקטים, הפניות לפי כיוון התלויות, ‏NuGet, ‏`.gitignore` | ✅ |
| 2 | Core | ישויות, Enums, ‏DTOs, ממשקים, ‏`Result`, קטלוג שגיאות | ✅ |
| 3 | Data | ‏DbContext, ‏Fluent API, ‏Repositories, ‏UnitOfWork, ‏Seed | ✅ |
| 4 | Service | לוגיקה עסקית, ‏AutoMapper, ‏JWT, גיבוב סיסמאות, **הטיפול בתחרות** | ✅ |
| 5 | API | ‏Controllers, ‏Middleware, ‏NLog, ‏Swagger, ‏`Program.cs` | ✅ |
| 6 | בדיקות | 55 בדיקות יחידה + 8 בדיקות concurrency | ✅ |
| 7 | לקוח React | 4 מסכים, כולל הצגת 409 | ✅ |
| 8 | Migrations | ‏`InitialCreate` + ‏`AddWaitlistAndIntegrityConstraints` | ✅ |
| 9 | הרצה מקומית | קומפילציה, 63/63 בדיקות, race demo, בדיקת API מלאה, בדיקת לקוח בדפדפן | ✅ |
| 10 | README | לשרת וללקוח | ✅ |
| 11 | GitHub | שני repositories נפרדים | ✅ (צריך להפוך ל-Public – סעיף 16) |

### Migrations
| # | שם | מה היא יוצרת |
|---|---|---|
| 1 | `InitialCreate` | כל הטבלאות, הקשרים, ועמודת `RowVersion` מסוג `rowversion` |
| 2 | `AddWaitlistAndIntegrityConstraints` | המונה `WaitlistCount`, השדה `PromotedAtUtc`, שלושת ה-CHECK constraints, והאינדקס הייחודי המסונן |

### Seed – נתוני הדמו
נוצרים **אוטומטית** בעלייה הראשונה (בסביבת Development), רק אם בסיס הנתונים ריק:
- 6 משתמשים (מנהלת + 5 משתתפות), 4 קטגוריות, 6 תגיות, 10 סדנאות, 25 מפגשים.
- **מצבים מוכנים להדגמה:** "אפיית חלות" – נשאר **מקום אחרון**; "קליגרפיה עברית" – **מלא עם רשימת המתנה**; "יסודות Excel" – מפגש עם **מקום יחיד**.
- מועדי המפגשים יחסיים ליום העלייה, כך שתמיד יש מפגשים עתידיים.

| תפקיד | מייל | סיסמה |
|---|---|---|
| Admin | `admin@workshophub.local` | `Admin#2026!` |
| Member | `rachel@workshophub.local` (וגם sara, chana, leah, miriam) | `Member#2026!` |

---

<a id="14"></a>
## 14. בדיקה מול ההנחיות – צ'קליסט סופי

> עברנו שוב, סעיף אחר סעיף, על מסמך ההנחיות ובדקנו כל דרישה מול הקוד ומול הרצה אמיתית.

### חלק א – בחירת הרעיון
| דרישה | ✓ | איפה / איך נבדק |
|---|:-:|---|
| משאב מוגבל שכמה משתמשים מתמודדים עליו באותו רגע | ✅ | מקום במפגש. race demo: ‏8 בקשות על מקום 1 → 201 אחד |

### חלק ב – 13 נושאי הקורס
| # | דרישה | ✓ | איפה | איך נבדק |
|---|---|:-:|---|---|
| 1 | REST: רבים, HTTP methods, אין פעולות ב-URL | ✅ | `API/Controllers` | 28 endpoints ב-Swagger |
| 1 | סטטוסים: ‏200, ‏201 עם Location, ‏204, ‏400, ‏401, ‏403, ‏404, ‏409 | ✅ | `ApiControllerBase`, ‏`CreatedAtAction` | כל אחד מהם הופק בבדיקת API אמיתית |
| 1 | Swagger פעיל ומתעד הכול | ✅ | `Program.cs` | 28 פעולות, כולן עם תיאור וסטטוסים |
| 2 | ‏`[FromRoute]` / `[FromQuery]` / `[FromBody]` | ✅ | כל ה-Controllers | קריאת הקוד |
| 2 | Data Annotations על DTOs + בדיקת ModelState | ✅ | `Core/DTOs`, ‏`ErrorResponseFactory` | בקשה לא תקינה → 400 עם פירוט לפי שדה |
| 2 | ולידציה עסקית ב-Service ולא ב-Controller | ✅ | `Service/Services` | מפגש בעבר → 400 מה-Service |
| 3 | 4 פרויקטים בתוך solution אחד | ✅ | `WorkshopHub.sln` | |
| 3 | Core לא מפנה לשום דבר | ✅ | `WorkshopHub.Core.csproj` | אין `PackageReference` |
| 3 | API מכיר את Data רק ב-Program.cs | ✅ | | חיפוש `AppDbContext` ב-API → רק `Program.cs` |
| 3 | אין Controller שמזריק DbContext | ✅ | | כנ"ל |
| 4 | הזרקה דרך constructor, אין `new` על service | ✅ | | חיפוש בקוד |
| 4 | Interfaces ב-Core, מימושים ב-Data וב-Service, רישום ב-Program.cs | ✅ | | |
| 4 | DbContext ‏Scoped + הסבר על Singleton שמחזיק Scoped | ✅ | `Program.cs` | סעיף 9 כאן + `ValidateScopes` |
| 5 | Code-First, ‏DbSet לכל ישות | ✅ | `AppDbContext` | 6 ישויות, 6 DbSet |
| 5 | **לפחות שתי migrations** | ✅ | `Data/Migrations` | 2 migrations |
| 5 | Connection string מהקונפיגורציה ולא בקוד | ✅ | `appsettings.Development.json` | |
| 5 | Seed שמאפשר להריץ מיד | ✅ | `Data/Seed` | עלייה ראשונה → כל הנתונים קיימים |
| 6 | CRUD מלא | ✅ | קטגוריות, סדנאות, מפגשים, הרשמות | |
| 6 | LINQ עם סינון ומיון | ✅ | `Data/Repositories` | |
| 6 | Pagination עם Skip/Take בתוך השאילתה | ✅ | `QueryableExtensions` | עמוד 2 שונה מעמוד 1, ‏`totalCount` נכון |
| 6 | ‏AsNoTracking בכל קריאה בלבד | ✅ | כל ה-Repositories | |
| 7 | one-to-many + many-to-many | ✅ | `Data/Configurations` | סדנה↔מפגשים, סדנה↔תגיות |
| 7 | Include / ThenInclude, ‏Fluent API | ✅ | | |
| 7 | אין N+1 | ✅ | `SessionService.AttachMyRegistrationsAsync` | בדיקת יחידה מוודאת שאילתה אחת לכל העמוד |
| 8 | ישויות לא יוצאות ולא נכנסות דרך ה-API | ✅ | `Core/DTOs` | ‏`PasswordHash` לא קיים בשום DTO |
| 8 | DTOs נפרדים ל-Request ול-Response | ✅ | | |
| 8 | AutoMapper Profiles | ✅ | `Service/Mapping/MappingProfile.cs` | `AssertConfigurationIsValid` בבדיקה |
| 9 | אסינכרוני מה-Controller עד ה-DbContext | ✅ | | אין `.Result` ואין `.Wait()` |
| 9 | ‏CancellationToken לאורך כל הדרך | ✅ | | כל action מקבל ומעביר אותו |
| 10 | Middleware לשגיאות עם JSON אחיד | ✅ | `ExceptionHandlingMiddleware` | ⚠️ ראו הערה 1 |
| 10 | Middleware ל-CorrelationId שנכנס ללוגים | ✅ | `CorrelationIdMiddleware` | מופיע בכל שורת לוג |
| 10 | סדר נכון: שגיאות ראשון, authentication לפני authorization | ✅ | `Program.cs` | |
| 11 | ‏appsettings + קובץ לסביבת פיתוח | ✅ | | |
| 11 | ‏IOptions | ✅ | `JwtSettings`, ‏`DatabaseSettings` | |
| 11 | **אין סוד בקוד ואין סוד ב-repo**, ‏User Secrets | ✅ | | ⚠️ ראו הערה 2 |
| 12 | הרשמה והתחברות, token עם claims, ולידציה בשרת | ✅ | `AuthService`, ‏`JwtTokenService` | token מזויף → 401 |
| 12 | שני roles עם הרשאות שונות בפועל | ✅ | Member / Admin | Member על endpoint של Admin → 403 |
| 12 | ‏`[Authorize]` | ✅ | | |
| 13 | xUnit + Moq על שכבת ה-Service | ✅ | `Tests/Unit` | 55 עוברות |
| 13 | **בדיקה על המשאב המוגבל – תקין וגם נדחה** | ✅ | `RegistrationServiceTests` | סעיף 11 |

### חלק ג – תחרות על משאב
| דרישה | ✓ | איפה |
|---|:-:|---|
| Optimistic concurrency | ✅ | |
| **concurrency token על הישות, מוגדר בקוד** | ✅ | `Session.RowVersion` + ‏`IsRowVersion()` |
| **‏`DbUpdateConcurrencyException` → 409 עם הודעה ברורה** | ✅ | `UnitOfWork` → ‏`RegistrationService` → ‏409 |
| ה-catch תופס חריגה ספציפית ולא `Exception` כללי | ✅ | `catch (DbUpdateConcurrencyException ex)` |
| הבדיקה העסקית והשמירה באותה טרנזקציה | ✅ | סעיף 6 |
| **בדיקה: שני DbContext, הראשון שומר, השני מקבל חריגה** | ✅ | `TwoDbContexts_...` – עוברת מול SQL Server אמיתי |
| פסקה ב-README: איזה משאב, איפה התחרות, איך המערכת מגיבה | ✅ | README של השרת |

### חלק ד – NLog
| דרישה | ✓ | איפה |
|---|:-:|---|
| חבילת `NLog.Web.AspNetCore` + ‏`nlog.config` | ✅ | `WorkshopHub.API` |
| לוג לכל בקשה נכנסת עם CorrelationId | ✅ | `RequestLoggingMiddleware` |
| לוג לכל חריגה שנתפסה – Error | ✅ | `ExceptionHandlingMiddleware` |
| לוג לכל התנגשות על משאב – Warning | ✅ | `RegistrationService`, ‏`SessionService` – נצפה בלוג האמיתי |
| רמות נכונות: ‏Debug / Information / Warning / Error | ✅ | Debug רק בפיתוח |
| בלי סיסמאות, tokens ותוכן בקשת הרשמה | ✅ | נבדק בקבצי הלוג |
| הקוד משתמש ב-`ILogger` הרגיל, ‏NLog מתחת | ✅ | |

### חלק ה – לקוח
| דרישה | ✓ |
|---|:-:|
| התחברות והרשמה ששומרות JWT ושולחות אותו ב-header | ✅ |
| מסך רשימה עם pagination אמיתי מול השרת | ✅ |
| מסך פעולה על המשאב המוגבל | ✅ |
| **הצגה ברורה של 409** | ✅ – נבדק בדפדפן |

### חלק ו – הגשה
| דרישה | ✓ |
|---|:-:|
| שני repositories נפרדים ב-GitHub | ✅ ‏`miri-margalit-server`, ‏`miri-margalit-client` |
| ‏`.gitignore` תקין – בלי bin, ‏obj, ‏node_modules וסודות | ✅ |
| README: תיאור המערכת | ✅ |
| README: המשאב המוגבל והטיפול בתחרות | ✅ |
| README: הוראות הרצה מקומית, כולל איזה בסיס נתונים | ✅ |
| README: משתמשי דמו לכל role | ✅ |
| **המערכת עולה בלי התערבות ידנית בבסיס הנתונים** | ✅ – ה-migrations וה-Seed רצים אוטומטית |

### "מה לא נכנס לפרויקט הזה"
לא הוספנו ענן, Docker, ‏S3, התחברות עם Google, ‏caching, ‏SignalR או CI – בדיוק לפי ההנחיות.
בונוסים שנוספו **על בסיס מלא**: רשימת המתנה עם קידום אוטומטי, הגנה על עריכת מנהלת עם RowVersion, ‏CHECK constraints, ‏race demo.

### הערות כנות – כדי שלא תופתעי
1. **מסלול ה-500 לא הופעל בבדיקה חיה.** ה-Middleware כתוב ומתועד, אבל במהלך הבדיקות לא קרתה אף חריגה לא צפויה, ולכן לא ראינו בעיניים תשובת 500. אם הבודקת תבקש להדגים – אפשר לעצור את SQL Server ולשלוח בקשה.
2. **סיסמאות משתמשי הדמו כתובות בקוד** (`Data/Seed/DemoData.cs`). הן **לא סודות**: ההנחיות דורשות לפרסם אותן ב-README, והן קיימות רק בסביבת הפיתוח. בבסיס הנתונים נשמר רק הגיבוב. הסודות האמיתיים (מפתח JWT וסיסמת בסיס נתונים) לא נמצאים ב-repo.
3. **AutoMapper כותב אזהרת רישיון** בעליית השרת. מגרסה 15 הספרייה מבקשת מפתח רישיון, אבל היא עובדת כרגיל גם בלעדיו. זה לא משפיע על הפרויקט, ומוסבר ב-README.

---

<a id="15"></a>
## 15. שאלות שכדאי לדעת לענות עליהן

**ש: מה המשאב המוגבל שלך, ואיפה התחרות?**
ת: מקום במפגש. התחרות היא כששתי משתתפות נרשמות לאותו מפגש באותו רגע, כשנשאר מקום אחד.

**ש: מה היה קורה בלי ההגנה?**
ת: שתיהן היו קוראות "נשאר מקום 1", שתיהן היו עוברות את הבדיקה ושתיהן היו שומרות. המפגש היה מלא מעבר לקיבולת, ובבסיס הנתונים לא היה שום סימן שמשהו השתבש.

**ש: למה בחרת optimistic ולא pessimistic?**
ת: ההנחיות דורשות optimistic. הוא גם מתאים יותר ל-web: לא נועלים שורות, אין המתנות ואין סכנת deadlock. ההתנגשות מזוהה בזמן השמירה ונדחית.

**ש: מה זה concurrency token, ואיך הוא עובד אצלך?**
ת: שדה שמשתנה בכל עדכון של השורה. אצלי זה `RowVersion` מסוג `rowversion` ש-SQL Server מעדכן לבד. EF Core מוסיף את הערך שנקרא לתנאי ה-WHERE של ה-UPDATE. אם מישהי שינתה את השורה בינתיים, אף שורה לא מתעדכנת ו-EF Core זורק `DbUpdateConcurrencyException`.

**ש: איפה בדיוק את תופסת את החריגה, ולמה לא ב-Service?**
ת: ב-`UnitOfWork` שבשכבת ה-Data. ה-Service מפנה רק ל-Core ולא מכיר את EF Core, ולכן שכבת ה-Data מתרגמת את החריגה ל-`ConcurrencyConflictException` של Core. ה-Service תופס אותה, כותב Warning ומחזיר Conflict, וה-API הופך את זה ל-409.

**ש: למה 409 ולא 400?**
ת: הבקשה עצמה תקינה. היא מתנגשת עם המצב הנוכחי של המשאב – וזו ההגדרה של 409.

**ש: איך הוכחת שזה עובד?**
ת: בבדיקה עם שני DbContext נפרדים מול SQL Server אמיתי: שניהם קוראים את אותו מפגש ומשנים אותו, הראשון שומר והשני מקבל `DbUpdateConcurrencyException`. בנוסף, 12 משתתפות נרשמות במקביל ל-3 מקומות, והמפגש אף פעם לא עובר את הקיבולת.

**ש: למה הבדיקה הזו לא רצה עם Moq?**
ת: כי `rowversion` הוא מנגנון של SQL Server. בדיקה מול נתונים מדומים לא מוכיחה שבסיס הנתונים באמת דוחה את השמירה השנייה.

**ש: למה הוספת `WaitlistCount` לשורת המפגש?**
ת: כדי שגם הצטרפות לרשימת ההמתנה תשנה את שורת המפגש. כך היא מתנגשת עם ביטול שקורה באותו רגע, ולא יכול להיווצר מצב שמשתתפת ממתינה בזמן שיש מקום פנוי.

**ש: למה Singleton שמחזיק Scoped הוא באג?**
ת: ה-Singleton נוצר פעם אחת לכל חיי האפליקציה. אם הוא מחזיק DbContext, הוא משתמש באותו DbContext לכל הבקשות. ה-DbContext לא thread-safe והמצב שלו מתיישן. הפעלתי `ValidateScopes` כדי שהשרת לא יעלה אם יש טעות כזו.

**ש: איפה הסודות שלך?**
ת: מפתח ה-JWT ב-User Secrets, מחוץ לתיקיית הפרויקט. ה-connection string הוא ל-LocalDB עם Windows Authentication, ולכן אין בו סיסמה.

**ש: מה זה CorrelationId ולמה צריך אותו?**
ת: מזהה ייחודי לכל בקשה. הוא מופיע בכל שורת לוג של הבקשה ובכל תשובת שגיאה, כך שאפשר לשחזר בקשה שלמה מתוך לוג משותף.

**ש: למה אין `ToList` לפני ה-pagination?**
ת: `ToList` טוען את כל הטבלה לזיכרון ורק אז חותך. אצלי `Skip` ו-`Take` נמצאים בתוך השאילתה, ובסיס הנתונים מחזיר רק את העמוד המבוקש.

**ש: מה זה N+1 ואיך נמנעת ממנו?**
ת: שאילתה אחת לרשימה ואז שאילתה נוספת לכל פריט. נמנעתי בעזרת `Include` / `ThenInclude`. למשל, את ההרשמות של המשתמשת לכל המפגשים שבעמוד אני מביאה בשאילתה אחת עם `WHERE SessionId IN (...)`.

---

<a id="16"></a>
## 16. מה נשאר לעשות לפני ההגשה

- [ ] **להפוך את שני ה-repositories ל-Public** (כרגע הם Private, והמרצה לא תוכל לפתוח אותם):
  ב-GitHub → נכנסים ל-repository → **Settings** → גוללים ל-**Danger Zone** → **Change visibility** → **Public**.
- [ ] לפתוח את שני הקישורים **בחלון גלישה בסתר** ולוודא שהם נפתחים:
  - `https://github.com/miri5202/miri-margalit-server`
  - `https://github.com/miri5202/miri-margalit-client`
- [ ] לוודא שבשרת מופיעה התיקייה `WorkshopHub.Data/Migrations` עם 5 קבצים, ושאין `bin` / `obj` / `node_modules`.
- [ ] להעלות את הקובץ הזה (`PLAN.md`) ל-repository של השרת: ב-GitHub Desktop → **Commit to main** → **Push origin**.
- [ ] לקרוא את סעיף 15 ולוודא שאת יודעת לענות על כל שאלה במילים שלך.

---

<a id="17"></a>
## 17. יומן החלטות

| החלטה | למה |
|---|---|
| רעיון: הרשמה לסדנאות עם מכסת מקומות ורשימת המתנה | המשאב (מקום במפגש) ברור, והתחרות עליו טבעית |
| ‏.NET 8 + ‏EF Core 8 | תואם לחבילות שכבר היו מותקנות במחשב |
| ‏SQL Server ‏LocalDB | מותקן במחשב, ויש לו `rowversion` מובנה בדיוק כמו בדוגמה שבהנחיות |
| Optimistic concurrency עם `rowversion` על `Session` | דרישת ההנחיות |
| התרגום של `DbUpdateConcurrencyException` נעשה ב-Data | כדי שה-Service לא יכיר את EF Core |
| `Result` במקום חריגות למצבים צפויים | ה-Service מחליט מה קרה, וה-Controller רק מתרגם לסטטוס |
| `WaitlistCount` על שורת המפגש | כדי שכל פעולה על הרשמות תתנגש על אותו token |
| קידום מרשימת ההמתנה באותה שמירה של הביטול | המקום לא "נעלם" ולא מוכפל |
| ‏`rowVersion` חוזר מהלקוח בעריכת מפגש | עריכה של מנהלת לא דורסת הרשמה שקרתה בינתיים |
| ‏CHECK constraints ואינדקס ייחודי מסונן | קו הגנה אחרון בבסיס הנתונים |
| בדיקות concurrency מול SQL Server אמיתי ולא Moq | רק כך הן מוכיחות משהו |
| PBKDF2 לגיבוב סיסמאות (מובנה ב-.NET) | בלי חבילה נוספת |
| התחברות = `POST /api/tokens` | REST: בלי שמות פעולות ב-URL |
| ה-migrations וה-Seed רצים בעליית השרת (Development) | "המערכת עולה בלי התערבות ידנית בבסיס הנתונים" |
| הלקוח מינימלי | ההנחיות: הלקוח לא נבדק |

</div>
