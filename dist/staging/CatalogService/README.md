# CatalogService Package

## What is included

- Source for `CatalogService`
- Shared contracts used by the service
- External database script: `database/CatalogDb.sql`

## Database setup

1. Open SQL Server Management Studio or Azure Data Studio.
2. Connect to your SQL Server instance.
3. Run `database/CatalogDb.sql`.
4. Verify the database name matches the service connection string.

## Default connection string

```json
Server=localhost,1433;Database=CatalogDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;Encrypt=False
```

## Run locally

```bash
dotnet restore
dotnet run --project src/CatalogService/CatalogService.csproj
```

## Run with Docker

```bash
docker build -f src/CatalogService/Dockerfile -t catalogservice .
docker run --rm -p 5001:8080 catalogservice
```

## Notes

- This service only needs SQL Server to start.
- When the whole system runs, it publishes stock changes to CirculationService.