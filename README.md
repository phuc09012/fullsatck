# BTL Fullstack Library Microservices

## Services

- `ApiGateway` on `http://localhost:5000`
- `CatalogService` on `http://localhost:5001`
- `CirculationService` on `http://localhost:5002`
- `IdentityReportService` on `http://localhost:5003`

## Quick Start

### Local run

1. Start a SQL Server instance on `localhost:1433`.
2. Run the services:

```bash
dotnet run --project src/CatalogService/CatalogService.csproj
dotnet run --project src/CirculationService/CirculationService.csproj
dotnet run --project src/IdentityReportService/IdentityReportService.csproj
dotnet run --project src/ApiGateway/ApiGateway.csproj
```

### Docker Compose

```bash
docker compose up --build
```

The SQL Server container is published on host port `1434` to avoid clashing with a local SQL Server on `1433`. Services inside Docker still use `sqlserver,1433`.

If you already have an external SQL Server and have imported the scripts from `database/`, run the app-only compose instead:

```bash
docker compose -f docker-compose.external.yml up --build
```

## Public Deployment

For a public website, use the production compose file:

```bash
docker compose -f docker-compose.public.yml up -d --build
```

This setup:

- serves the frontend from Nginx on port `80`
- keeps the backend services and SQL Server private inside Docker
- proxies `/api` from the frontend to the API gateway on the same host

Before going live, change the secret values in your environment if needed:

- `MSSQL_SA_PASSWORD`
- `JWT_KEY`
- `INTERNAL_API_KEY`

If you deploy on a VPS, point your domain to that server and open port `80` in the firewall. If you only want a temporary public demo, you can tunnel the frontend port with a tool like Cloudflare Tunnel.

### Fixed URL with Cloudflare Tunnel

If you want a URL that stays the same after restarts, use a named Cloudflare Tunnel instead of a quick tunnel.

1. Create a tunnel in Cloudflare Zero Trust and copy its token.
2. Set `CLOUDFLARE_TUNNEL_TOKEN` in your environment.
3. Start the stack with the tunnel overlay:

```bash
docker compose -f docker-compose.public.yml -f docker-compose.cloudflare.yml up -d --build
```

The hostname you map in Cloudflare stays fixed, so the public URL no longer changes every time you restart the demo.

## External Database Setup

If you want to run the services against a SQL Server that sits outside the apps, use the scripts in [`database`](./database):

1. Create and seed `CatalogDb` with [`database/CatalogDb.sql`](./database/CatalogDb.sql)
2. Create and seed `IdentityDb` with [`database/IdentityDb.sql`](./database/IdentityDb.sql)
3. Create and seed `CirculationDb` with [`database/CirculationDb.sql`](./database/CirculationDb.sql)

The services already point to separate database names by default:

- `CatalogService` -> `CatalogDb`
- `CirculationService` -> `CirculationDb`
- `IdentityReportService` -> `IdentityDb`

If your SQL Server is not on `localhost:1433`, update the connection strings in the service `appsettings.json` files or override them with environment variables.

## Seed Accounts

- `admin@library.local` / `Admin@123`
- `librarian@library.local` / `Librarian@123`
- `reader1@library.local` / `Reader@123`
- `reader2@library.local` / `Reader@123`
- `reader3@library.local` / `Reader@123`

## Demo Data

When the services start on a fresh database, each service seeds its own demo data automatically:

- `CatalogService`: 5 books with current stock states
- `CirculationService`: 3 sample borrowing records and matching catalog snapshots
- `IdentityReportService`: staff accounts, 3 reader accounts, and reader profiles

If you want to reset everything locally, remove the SQL Server volume and start Docker Compose again.

## Service Handoff

Each service is designed to run independently as long as its database exists:

- `CatalogService` manages books, book categories, and stock state.
- `CirculationService` manages borrow/return transactions, borrowing rules, overdue fines, and fine payments.
- `IdentityReportService` manages auth, users, readers, report projections, and dashboard data.
- `ApiGateway` routes requests and handles JWT auth.

For team handoff, send the repository together with the `database/` folder so they can import the schema/data first and run the services afterward.

See [`handoff/TEAM_SPLIT.md`](./handoff/TEAM_SPLIT.md) for a team-by-team split checklist.

## Completed Topic 02 Scope

- Catalog: CRUD books, managed book categories, cover image, description/content notes, online metadata import from Open Library, copy counts, archive/restore, low-stock summary, category list, search by title/author/category/ISBN/publisher/description/content/year.
- Circulation: borrow, return, renew, reader history, overdue list, configurable borrowing rules, fine calculation, fine payment, catalog/reader validation through service APIs.
- Identity & Report: JWT login/register, Admin user management, reader profiles/card expiry/status, dashboard, reader reports, overdue readers, top books, event projections.
- Security: Gateway JWT validation plus service-level JWT validation for protected endpoints; internal service-to-service endpoints require `X-Internal-Api-Key`.
- Frontend: Vue 3 + Vuetify 3 bootstrap + Pinia dashboard with role-aware catalog, Admin category manager, category dropdown for book forms, online book import panel, borrowing, returns, fine payment, borrowing-rule panel, reader/account summaries.

Online import uses public metadata/preview data. Do not store full copyrighted book text unless the source explicitly permits it.

## Notes

- JWT issuer/audience/key are shared across all services.
- `InternalApi:Key` must match across Catalog, Circulation, and Identity so internal API calls/events are accepted.
- Each service uses its own SQL Server database:
  - `CatalogDb`
  - `CirculationDb`
  - `IdentityDb`
- The gateway authenticates JWT and routes to the corresponding service.
