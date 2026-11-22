# Authentication API (KAN-70)

The API uses Identity session cookies. Use relative `/api` URLs through Vite in development. No bearer token or browser CORS configuration is needed.

| Endpoint | JSON body | Success |
| --- | --- | --- |
| `GET /api/auth/csrf` | None | 200: `{ requestToken, headerName }` |
| `POST /api/auth/register` | `{ username, displayName, password, email?, avatarKey? }` | 201: user profile; signs in |
| `POST /api/auth/login` | `{ username, password }` | 200: user profile; signs in |
| `POST /api/auth/logout` | None | 204; requires authentication |
| `GET /api/auth/me` | None | 200: current user profile; requires authentication |

Profiles contain only `id`, `username`, `displayName`, `email`, and `avatarKey`. Responses are not cached. Passwords and EF entities are never returned.

Username and display name must be 3–20 characters. Identity also validates username characters and requires passwords of at least 8 characters with uppercase, lowercase, and a digit. Email is optional, need not be unique, and requires no confirmation. Avatars are `sunrise` (default), `berry`, `meadow`, or `ocean`.

Validation and invalid CSRF tokens return 400 Problem Details; validation includes an `errors` dictionary. Duplicate usernames return 409. Invalid credentials, including locked accounts, return the same generic 401 response. Unauthenticated protected endpoints return 401 without redirects. Identity's existing lockout rules apply to failed logins.

## React request flow

The token/cookie pair uses [ASP.NET Core antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

1. Fetch `/api/auth/csrf` with `credentials: 'include'`. The browser stores the HttpOnly `JobTracker.Antiforgery` cookie; keep the returned request token in memory.
2. Send `X-CSRF-TOKEN: <requestToken>` on **every auth POST**, including registration and login. JSON bodies use `Content-Type: application/json`. Include cookies with `credentials: 'include'`.
3. Successful registration/login sets the HttpOnly `JobTracker.Auth` session cookie. Fetch a **new CSRF token after registration, login, or logout**, because tokens are bound to the current identity. Refresh the token after a CSRF failure before retrying.
4. Call `/api/auth/me` with cookies to restore the current session. JavaScript does not read either cookie.

```ts
const { requestToken } = await fetch('/api/auth/csrf', {
  credentials: 'include',
}).then(response => response.json())

const response = await fetch('/api/auth/login', {
  method: 'POST',
  credentials: 'include',
  headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': requestToken },
  body: JSON.stringify({ username, password }),
})
// Handle response.status, then refresh the CSRF token after successful login.
```

Auth cookies use SameSite=Lax; antiforgery cookies use SameSite=Strict. Development supports HTTP through Vite; other environments require HTTPS cookies. Keep production frontend and API on one origin. Swagger users must also obtain the CSRF token and supply its header with cookies for POST requests.

## Reproducible API checks

`JobTracker.Tests/Auth/AuthEndpointsTests.cs` checks the authentication API through HTTP using an in-memory test database. Run it with `dotnet test`. Shared test infrastructure lives in `JobTracker.Tests/TestSupport`.

The existing `scripts/check-auth.py` also uses Python 3's standard library to check validation, duplicate usernames, generic login failures, sign-in cookies, current user, logout, and missing/invalid/stale/mismatched CSRF tokens against a running API.

For that script, point the API at a **disposable PostgreSQL database**, then start the API using the HTTP profile; migrations run on startup. Do not run this against real user data: it creates two uniquely named test users and does not delete them.

```bash
python3 scripts/check-auth.py http://localhost:5099 --allow-test-users
```

The script prints status checks, never credentials or cookie/token values. No schema or migration changes are required by KAN-70. React screens and application CRUD are separate work.
