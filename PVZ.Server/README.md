# PVZ Server

`PVZ.Server` is a separate ASP.NET Core service for online taskbooks. It stores integrity-checked `.pvz` packages, registers teacher accounts, issues six-character student tokens, and records student identity and progress. The Russian teacher dashboard is available at `/admin` (the root `/` redirects there).

## Shared deployment

The service binds to `http://0.0.0.0:5080` by default, but rejects non-HTTPS requests from remote clients. It therefore needs a public host/domain and a TLS endpoint before clients on other computers can use it. Recommended deployment is a TLS reverse proxy (for example, Nginx or Caddy) on the same host, forwarding to the service over loopback:

```powershell
$env:PVZ_SERVER_DATA_DIR = 'D:\PVZServerData'
$env:PVZ_TOKEN_LIFETIME_HOURS = '48'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
dotnet run --project PVZ.Server\PVZ.Server.csproj
```

Configure the proxy to serve HTTPS and forward `X-Forwarded-For` and `X-Forwarded-Proto`; only trusted proxies are honored. The default ASP.NET Core trusted proxy list includes loopback. If a proxy is on another IP, add its exact address to `PVZ_TRUSTED_PROXIES` as a comma-separated list. Alternatively, configure Kestrel directly with HTTPS and a managed certificate, then bind `ASPNETCORE_URLS` to `https://0.0.0.0:5080`; provide the certificate using secure deployment configuration, never source code.

Use a public DNS name, valid TLS certificate, firewall rules that expose only the HTTPS proxy/Kestrel port, persistent writable storage, and backups. Configure `PVZ_SERVER_DATA_DIR` on durable storage and restrict it to the server account. Run one server process per data directory; the JSON store is designed for a single instance, not a multi-node deployment.

Teacher registration is open and limited to five registrations per client IP per hour. A teacher receives a randomly generated eight-character personal API key once; only its SHA-256 hash is stored. The key grants access only to that teacher's packages and student sessions. Teachers should keep it private and configure it in the builder as `PVZ_TEACHER_API_KEY` or paste it when prompted. It is distinct from the teacher password stored in a taskbook and must never be given to students. Because registration is public and does not yet verify email/identity, production operators should add abuse controls (such as a reverse-proxy registration limit or CAPTCHA) before broad public launch.

The default token lifetime is 48 hours; override with `PVZ_TOKEN_LIFETIME_HOURS` (1 through 87600) or legacy `PVZ_TOKEN_LIFETIME_DAYS` (1 through 3650). Student tokens are six characters from `A–Z` and `2–9` (for example `P67VZ7`); the mix of letters and digits is random. Request rate limits mitigate guessing; retain HTTPS and consider additional edge rate limiting for public deployment.

## Desktop client configuration

Desktop clients resolve the server from `https://pvzserver.vercel.app/` unless `PVZ_SERVER_URL` is set. The teacher builder uses `PVZ_TEACHER_API_KEY`, or asks for the personal key when the variable is absent. Students enter a six-character taskbook token, name, surname, and class; the app reports progress periodically while the taskbook is open. The dashboard refreshes teacher-owned tokens and student progress every three seconds; a student is considered online while a heartbeat was received within the last 30 seconds.

Online student records contain the supplied name, surname, class, score, progress, start time, and last activity. They are stored as JSON on disk without at-rest encryption and are retained until the operator manages the server data directory; there is no per-student retention control yet. Scores are reported by the student client and are not tamper-proof or suitable as trusted grades without server-side execution/verification. Operators should publish a privacy notice and restrict filesystem access appropriately.

## API

- `GET /health`: liveness.
- `GET /` and `GET /admin`: teacher registration and dashboard.
- `POST /api/teachers/register`: register by display name and receive the personal API key once.
- `POST /api/teacher/taskbooks`: teacher-authenticated `.pvz` upload; returns the six-character token once.
- `GET /api/teacher/taskbooks`: list only the authenticated teacher's token hints and lifecycle state.
- `GET /api/teacher/sessions`: list only the authenticated teacher's student identity and progress summaries.
- `DELETE /api/teacher/taskbooks/{id}`: revoke an owned token.
- `POST /api/taskbooks/{token}/sessions`: create a student session with identity.
- `PUT /api/student/sessions/{sessionId}/progress`: report task scores/completion; the high-entropy session id authorizes the session.
- `GET /api/taskbooks/{token}`: download the active package.

Packages are compressed/integrity-checked, not encrypted. HTTPS protects transport. Package encryption is not implemented because a safe key-delivery design has not been established. The server stores token hashes and a short token hint, not recoverable full tokens; losing a token requires uploading the taskbook again. Revoked or expired tokens return 404.

## Integration test

From the repository root, run:

```powershell
dotnet build PVZ.sln -c Debug
dotnet run --project PVZ.Server.Tests\PVZ.Server.Tests.csproj -c Debug
```

The integration executable starts a temporary local server and checks redirects, registration, teacher isolation, upload/package validation, six-character token download, student identity/progress, persistence across restart, revocation, and expiry.
