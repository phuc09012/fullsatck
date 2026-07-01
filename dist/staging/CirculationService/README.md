# CirculationService Package

## What is included

- Source for `CirculationService`
- Shared contracts used by the service
- External database script: `database/CirculationDb.sql`

## Database setup

1. Open SQL Server Management Studio or Azure Data Studio.
2. Connect to your SQL Server instance.
3. Run `database/CirculationDb.sql`.
4. Verify the database name matches the service connection string.

## Default connection string

```json
Server=localhost,1433;Database=CirculationDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False
```

## Run locally

```bash
dotnet restore
dotnet run --project src/CirculationService/CirculationService.csproj
```

## Run with Docker

```bash
docker build -f src/CirculationService/Dockerfile -t circulationservice .
docker run --rm -p 5002:8080 circulationservice
```

## Notes

- This service needs CatalogService reachable at `CatalogService__BaseUrl`.
- When the whole system runs, it publishes borrow/return events to IdentityReportService.