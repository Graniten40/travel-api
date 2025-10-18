# Travel API (.NET 9 + EF Core + SQL Server via Docker)

# Ett enkelt REST API för sevärdheter, byggt med ASP.NET Core 9, Entity Framework Core och SQL Server i Docker.

# Förutsättningar

# Docker Desktop

# .NET SDK 9.0

# (Git, om du klonar från GitHub)

# Kom igång (snabbguide)

# Starta SQL Server i Docker

# docker compose up -d


# Containern ska visas som healthy i docker ps. Port 1433 ska vara exponerad.

# Konfigurera connection string (lokal körning)
# I Travel.Api/appsettings.Development.json:

# {
#  "ConnectionStrings": {
#    "DefaultConnection": "Server=tcp:127.0.0.1,1433;Database=TravelDb;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
#  }
# }


# Alternativt (rekommenderas) – lagra lokalt med User Secrets:

# dotnet user-secrets init --project Travel.Api
# dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=tcp:127.0.0.1,1433;Database=TravelDb;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;" --project Travel.Api


# Kör migreringar (skapar databasen)

# dotnet ef database update -p Travel.Infrastructure -s Travel.Api


# Starta API:t

# dotnet run --project Travel.Api


# Swagger: http://localhost:5030/swagger

# Nyttiga endpoints

# GET /api/attractions – lista/filter/paginera sevärdheter

# GET /api/attractions/{id}/comments – hämta kommentarer

# POST /api/attractions/{id}/comments – skapa kommentar

# Projektstruktur
# Travel.Api/              # Web API (ASP.NET Core)
# Travel.Infrastructure/   # EF Core (DbContext, migrationer)
# Travel.Domain/           # Domänmodeller, DTO:er
# external/SeedGenerator/  # (valfritt) seed-exempel
# docker-compose.yml       # SQL Server-container

# Vanliga problem & lösningar

# A) “Error 53 – Could not open a connection to SQL Server”

# Se att containern kör och är healthy: docker ps

# Testa porten: Test-NetConnection 127.0.0.1 -Port 1433 (PowerShell)

# Använd exakt Server=tcp:127.0.0.1,1433;... när API:t körs lokalt

# B) Migrationer klagar på index på textkolumn

# Kolumnen Attractions.Title är begränsad till nvarchar(200) (indexerbar).

# Om du byter databas från SQLite → SQL Server: radera gamla migrationer och skapa en ny “InitSqlServer”.

# C) Docker compose-varning om version:

# Raden är obsolet och kan tas bort utan påverkan.

# Helt i Docker (valfritt)

# Om du vill köra API + SQL i Docker, lägg till en Dockerfile för Travel.Api och en extra service i docker-compose.yml.
# Connection string i Docker-nätverket:
# Server=sqlserver,1433;Database=TravelDb;User Id=sa;Password=YourStrong!Passw0rd;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;

# API:t kan köra Database.Migrate() vid uppstart, så DB skapas automatiskt.

# Licens

# Johan Calarion Persson.
