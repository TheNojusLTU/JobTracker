# Job Application Tracker

A Software Engineering course project for tracking job applications.

- `JobTracker`: ASP.NET Core (.NET 10) backend API with PostgreSQL, EF Core, and Identity.
- `JobTracker.Web`: separate React + TypeScript frontend using Vite.

Planned features include accounts, application tracking, search, interviews, and dashboard statistics.

## Setup

Requires .NET 10, PostgreSQL, Node.js 22.x, and npm 10+.

Run commands from the repository root. Start PostgreSQL and create a database named `jobtracker`. Set your local connection string, replacing the username and password as needed:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=jobtracker;Username=postgres;Password=YOUR_LOCAL_PASSWORD" --project JobTracker/JobTracker.csproj
```

Apply the existing database migrations:

```bash
dotnet ef database update --project JobTracker/JobTracker.csproj --startup-project JobTracker/JobTracker.csproj
```

Keep database credentials out of the repository.

## Run

Start the API:

```bash
dotnet run --project JobTracker/JobTracker.csproj --launch-profile http
```

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

## Checks

```bash
dotnet build
npm --prefix JobTracker.Web run lint
npm --prefix JobTracker.Web run build
```
