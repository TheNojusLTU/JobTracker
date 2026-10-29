# Project Requirements Checklist

This checklist maps the supplied course requirements to implementation and identifies requirements that cannot be proven by code alone.

| # | Requirement | Status | Implementation and evidence |
|---|---|---|---|
| 1 | Interactive user interface | Met | `JobTracker.Web` is a React application with registration/login, an authenticated application dashboard, search and status filters, pagination, create/edit forms, status changes, and CSV import. Main UI: `JobTracker.Web/src/App.tsx`; authentication: `JobTracker.Web/src/AuthScreen.tsx`. |
| 2 | Define a class, struct, record, and enum; include an immutable type | Met | `JobTracker/Models/JobApplication.cs` defines the application entity; `JobTracker/Models/DateRange.cs` is a `readonly struct`; API/auth contracts are records, one per file in `JobTracker/Contracts/` (for example `ApplicationRequest.cs`, `PagedResponse.cs`); `JobTracker/Models/ApplicationStatus.cs` defines the status enum. The `readonly` date-range value type is immutable. |
| 3 | Properties in both a struct and a class | Met | `DateRange` exposes its date bounds as properties in `JobTracker/Models/DateRange.cs`; `JobApplication` has entity properties in `JobTracker/Models/JobApplication.cs`. |
| 4 | Named and optional arguments | Met | Optional arguments include `ApplicationRequest.Validate(bool requireStatus = false)` and the optional filter/paging parameters in `ApplicationEndpoints.GetApplications`. Named argument usage includes CSV parsing and service/API calls in `JobTracker/Api/ApplicationEndpoints.cs`. |
| 5 | Extension methods | Met | Application search, filtering, sorting, and paging are implemented as extensions in `JobTracker/Extensions/` (including `PagingExtensions.cs`). |
| 6 | Correct collection iteration | Met | Collection work uses typed `foreach`/LINQ and React `.map` rendering; examples include CSV row processing in `JobTracker/Api/ApplicationEndpoints.cs`, query composition in `JobTracker/Extensions/`, and application rendering in `JobTracker.Web/src/App.tsx`. |
| 7 | Stream-based data loading | Met | The authenticated CSV import reads the HTTP request body in bounded chunks from `HttpRequest.Body` before parsing it, with a 1 MB cap and a 500-row cap. Implementation: `JobTracker/Api/ApplicationEndpoints.cs`; UI and API helper: `JobTracker.Web/src/App.tsx` and `JobTracker.Web/src/api.ts`. |
| 8 | LINQ to Objects, or justify its absence | Met | LINQ is used for the server-side application query and in-memory collections. The `IEnumerable<JobApplication>` search/filter/sort extension overloads in `JobTracker/Extensions/` operate over in-memory sequences (via `AsQueryable`/`EnumerableQuery`); no absence justification is needed. |
| 9 | Implement a standard .NET interface | Met | The immutable `DateRange` value type implements `IEquatable<DateRange>` with value equality and a matching hash code in `JobTracker/Models/DateRange.cs`. This avoids changing the JSON serialization shape of the paged API response. |
| 10 | PR descriptions, at least 3 merged PRs per member, and at least 3 meaningful peer reviews per member | Not established by source code | These are collaboration/history requirements, not runtime features. At the time of the repository-history check, merged PR counts were Splexis: 10, mliandzbergas: 8, TheNojusLTU: 6, and bbernardaslib: 1. An inline-comment scan found substantive-comment proxies for Splexis: 3, mliandzbergas: 5, TheNojusLTU: 1, and bbernardaslib: 0. Inline comments are not a definitive count of meaningful peer reviews; check the team's roster and GitHub review history before claiming this requirement is met. |
| 11 | Uniform code style | Met | Repository-root `.editorconfig` defines whitespace cleanup and consistent indentation for C# and frontend source. The .NET whitespace formatter and frontend ESLint both pass. |

## Verification

Run from the repository root:

```bash
dotnet build
dotnet format whitespace JobTracker.sln --verify-no-changes
npm --prefix JobTracker.Web run lint
npm --prefix JobTracker.Web run build
```

CSV import is intentionally atomic: if any row is invalid, validation errors are returned and no rows are saved. It requires authentication and the same CSRF token as other application write operations.
