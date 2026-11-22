# Job Application Tracker

A Software Engineering course project for tracking job applications.

- `JobTracker`: ASP.NET Core (.NET 10) backend API with PostgreSQL, EF Core, and Identity.
- `JobTracker.Web`: separate React + TypeScript frontend using Vite.

Features include accounts, application tracking, search, and dashboard statistics.

## Setup

Requires .NET 10, PostgreSQL, Node.js 22.x, and npm 10+.

Run commands from the repository root. Start PostgreSQL and create a database named `jobtracker`. Set your local connection string, replacing the username and password as needed:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=jobtracker;Username=postgres;Password=YOUR_LOCAL_PASSWORD" --project JobTracker/JobTracker.csproj
```

Keep database credentials out of the repository.

## Run

Start the API:

```bash
dotnet run --project JobTracker/JobTracker.csproj --launch-profile http
```

The API applies pending EF Core migrations before accepting requests in every environment.
Restarting an up-to-date database preserves its data and does not reapply migrations.
The database user must have permission to update the schema. If the database is
unavailable or a migration fails, the API logs the failure and exits without starting.
Creating new migrations still requires `dotnet ef migrations add`; running
`dotnet ef database update` manually is no longer needed for startup.

Start the frontend in another terminal:

```bash
npm --prefix JobTracker.Web install
npm --prefix JobTracker.Web run dev
```

Open the frontend URL printed by Vite. Requests to `/api` are forwarded to `http://localhost:5099`, so local development does not need browser CORS configuration.

## API Foundation (KAN-69)

- Health: `GET http://localhost:5099/api/health` returns **200** and `{"status":"healthy"}`. It checks the API host, not the database.
- Swagger: `http://localhost:5099/swagger` in Development; OpenAPI JSON is at `/openapi/v1.json`.
- Errors: API errors use JSON Problem Details with a request path and trace ID.

The server no longer hosts Blazor pages. The old Blazor UI and the `JobTracker.Client` project were removed in KAN-74.

Database models and migrations are preserved. Cookie authentication APIs are implemented in KAN-70; see [authentication endpoints, CSRF setup, and API checks](docs/authentication.md). The React frontend provides registration, login, and cookie-backed session handling. Authenticated users can list, search/filter, create, and edit applications, including updating their status, through the KAN-71 API. Application write requests include the API's required CSRF token.

The React application list also supports importing CSV files through `POST /api/applications/import`. The CSV must have these columns, in order: `CompanyName,JobTitle,Location,Salary,JobUrl,ApplicationDate,Status,Notes`. Use `YYYY-MM-DD` dates, invariant decimal notation, and a valid status name; optional values may be left blank. Imports are limited to 1 MB and 500 rows and are all-or-nothing.

Only one CSV import per user may run at a time, including across API instances using the same PostgreSQL database. An overlapping request receives HTTP 409 Problem Details; retry after the first import finishes. Different users may import concurrently. The guard does not prevent duplicate imports submitted after a prior import completes. A transaction and database connection remain open while the request is read and processed, so configure appropriate upload and database idle-in-transaction timeouts. The advisory-lock key is a stable 64-bit hash of the user ID; a hash collision could exceptionally reject an unrelated user's import.

## Dashboard statistics

The overview summarizes all of the signed-in user's applications regardless of list filters or pagination. It counts applications by current status and by application date (one row per month with applications, newest first).

Interview and offer totals count each application that reached the corresponding status once, even if its status changed later. The application-to-interview rate divides the interview total by all tracked applications. The interview-to-offer rate divides the number of applications with both milestones by the number that reached Interview. A rate with no denominator is shown as N/A.

On upgrade, existing applications get milestone credit for their current Interview or Offer status. Earlier transitions were not stored and cannot be recovered.

## Favorites and priority

The Favorite and Priority buttons in the application list toggle independent flags. Use the priority filter to show only priority or only non-priority applications. The API applies the filter before paginating the results.

## Checks

Application routes are implemented in `JobTracker/Controllers/ApplicationsController.cs`.
The controller receives application and CSV import service interfaces through its
constructor; authentication endpoints use `IAuthService`. Implementations are
registered in `Program.cs`.

`JobTracker.Tests` mirrors the backend folders (`Api`, `Auth`, `Controllers`, `Data`,
`Extensions`, `Models`, and `Services`). Shared fixtures and HTTP helpers live in
`TestSupport`; controller and endpoint tests exercise the API through HTTP.

```bash
dotnet build
dotnet test
npm --prefix JobTracker.Web run lint
npm --prefix JobTracker.Web run build
```

PostgreSQL-specific import concurrency tests require a **disposable, empty database**. Set `JOBTRACKER_TEST_POSTGRES` to its connection string before running `dotnet test`; the tests create tables and test users in that database. Without this setting those tests are skipped, unless `JOBTRACKER_REQUIRE_POSTGRES_TESTS` is `true`, in which case they fail instead. CI starts a disposable PostgreSQL service and requires these tests.
