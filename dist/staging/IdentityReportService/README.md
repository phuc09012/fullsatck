# IdentityReportService Package

## What is included

- Source for `IdentityReportService`
- Shared contracts used by the service
- External database script: `database/IdentityDb.sql`

## Database setup

1. Open SQL Server Management Studio or Azure Data Studio.
2. Connect to your SQL Server instance.
3. Run `database/IdentityDb.sql`.
4. Verify the database name matches the service connection string.

## Default connection string

```json
Server=localhost,1433;Database=IdentityDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False
```

## Run locally

```bash
dotnet restore
dotnet run --project src/IdentityReportService/IdentityReportService.csproj
```

## Run with Docker

```bash
docker build -f src/IdentityReportService/Dockerfile -t identityreportservice .
docker run --rm -p 5003:8080 identityreportservice
```

## Notes

- This service only needs SQL Server to start.
- It issues JWT tokens and stores reader/report projections.
- Demo accounts are seeded in the SQL script.