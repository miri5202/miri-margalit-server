# Dockerfile לפרסום השרת ב-Render (ובכל ענן שמריץ קונטיינרים).
# שלב 1: בנייה עם ה-SDK. שלב 2: הרצה על image קטן שיש בו רק את ה-runtime.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# קודם רק קבצי הפרויקטים – כך שלב ה-restore נשמר ב-cache כל עוד התלויות לא השתנו
COPY WorkshopHub.Core/WorkshopHub.Core.csproj       WorkshopHub.Core/
COPY WorkshopHub.Data/WorkshopHub.Data.csproj       WorkshopHub.Data/
COPY WorkshopHub.Service/WorkshopHub.Service.csproj WorkshopHub.Service/
COPY WorkshopHub.API/WorkshopHub.API.csproj         WorkshopHub.API/
RUN dotnet restore WorkshopHub.API/WorkshopHub.API.csproj

COPY WorkshopHub.Core/    WorkshopHub.Core/
COPY WorkshopHub.Data/    WorkshopHub.Data/
COPY WorkshopHub.Service/ WorkshopHub.Service/
COPY WorkshopHub.API/     WorkshopHub.API/
RUN dotnet publish WorkshopHub.API/WorkshopHub.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# NLog כותב לתיקייה logs; המשתמש app (לא root) צריך הרשאת כתיבה אליה. הלוגים מופיעים גם ב-console.
RUN mkdir -p /app/logs && chown -R $APP_UID /app/logs
USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "WorkshopHub.API.dll"]
