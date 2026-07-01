# Topic 02 Team Split

This project is structured for three independent student teams plus one shared gateway/frontend layer.

## Shared Contracts

All teams share `src/Shared.Contracts` for DTOs, roles, event names, and internal header names. Do not copy/paste divergent versions of these contracts when splitting repositories.

Shared configuration that must match:

- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:Key`
- `InternalApi:Key`

## Team 1 - Catalog Service

Owns:

- `src/CatalogService`
- `database/CatalogDb.sql`
- `handoff/CatalogService`

Main capabilities:

- CRUD books with ISBN/title/author/publisher/year/category/cover/description.
- Manage book categories so staff choose normalized category options when creating or editing books.
- Store description/content notes and import public metadata/preview data from Open Library.
- Manage total/available/minimum copies.
- Archive/restore books.
- Search books by keyword and year.
- Publish `book.availability.changed` to Circulation.
- Protect staff writes with JWT roles `Admin,Librarian`.
- Accept internal borrow/return stock changes only with `X-Internal-Api-Key`.

## Team 2 - Circulation Service

Owns:

- `src/CirculationService`
- `database/CirculationDb.sql`
- `handoff/CirculationService`

Main capabilities:

- Create borrow records after checking reader and catalog state.
- Return and renew borrow records.
- Configure borrowing policy: max active borrowings, default days, max renewal days, fine/day, reader self-checkout.
- Compute overdue fines.
- Record fine payments and remaining debt.
- Publish `book.borrowed`, `book.returned`, and `fine.paid`.
- Consume `book.availability.changed`.

## Team 3 - Identity & Report Service

Owns:

- `src/IdentityReportService`
- `database/IdentityDb.sql`
- `handoff/IdentityReportService`

Main capabilities:

- JWT login and reader registration.
- Admin user management.
- Reader profile, library card, expiry, active/locked status.
- Internal reader lookup for Circulation.
- Consume borrowing/fine events into report projections.
- Dashboard, reader report, overdue-reader report, top borrowed books.

## Shared Gateway and Frontend

Owns:

- `src/ApiGateway`
- `frontend`
- `docker-compose.yml`
- `docker-compose.external.yml`

The gateway is the public entry point. The frontend should call only the gateway base URL, normally `http://localhost:5000`.
