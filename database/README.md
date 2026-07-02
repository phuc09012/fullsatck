## External Database Scripts

These scripts are meant for SQL Server instances running outside the application.

Files:

- `CatalogDb.sql`
- `CirculationDb.sql`
- `IdentityDb.sql`
- `ERD.md` — sơ đồ bảng của 3 database

Suggested order:

1. Create and seed `CatalogDb`
2. Create and seed `IdentityDb`
3. Create and seed `CirculationDb`

Connection strings in the services already point to separate database names:

- `CatalogDb`
- `CirculationDb`
- `IdentityDb`

If you want a clean reset, drop the three databases and rerun the scripts.

Schema notes:

- `CirculationDb` includes `BorrowingPolicies` so Admin can configure borrowing limits and fine rules.
- `CirculationDb.BorrowingRecords` and `IdentityDb.BorrowingProjections` include fine payment fields for paid/outstanding debt.
- The scripts are idempotent for the main tables/columns used by the current codebase.
- See [`ERD.md`](./ERD.md) for a visual table diagram of all three databases.
