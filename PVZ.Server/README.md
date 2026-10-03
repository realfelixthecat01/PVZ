# PVZ Server

PVZ.Server is a separate ASP.NET Core service for online taskbook delivery. It accepts an integrity-checked `.pvz` package, issues a random student token, stores the package and token metadata on disk, and lets a student download by token. The admin page at `/admin` lists token hints, expiry/revocation state, supports upload, and allows revocation.

## Run

Install the .NET 10 ASP.NET Core runtime (or publish the app self-contained). Configure a unique admin API key of at least 32 UTF-8 bytes and a persistent writable data directory:

```powershell
$env:PVZ_SERVER_ADMIN_KEY = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$env:PVZ_SERVER_DATA_DIR = 'D:\PVZServerData'
$env:PVZ_TOKEN_LIFETIME_DAYS = '90'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
dotnet run --project PVZ.Server\PVZ.Server.csproj
```

The loopback URL is the safe default for a server used only on the same PC. To serve other PCs, configure Kestrel HTTPS (including its certificate) or use a loopback reverse proxy that forwards the HTTPS scheme, set the server URL accordingly, and open only the required firewall port. The server rejects non-HTTPS requests from non-loopback clients; forwarded headers are honored only from ASP.NET Core's trusted proxy list. Do not publish the admin API key in source control or distribute it to students.

The default token lifetime is 90 days; `PVZ_TOKEN_LIFETIME_DAYS` accepts 1 through 3650. Store the data directory on persistent storage and restrict filesystem access to the server account. Run one server process per data directory. Token records contain SHA-256 hashes and a short hint, not recoverable full tokens. The full student token is returned once on upload; losing it requires uploading again. Revoked/expired tokens return 404.

## Desktop client configuration

Set these environment variables on the teacher PC before launching the builder:

- `PVZ_SERVER_URL`: server origin, for example `https://pvz.example.edu`; defaults to `http://localhost:5080`.
- `PVZ_SERVER_ADMIN_KEY`: the server admin key. Used for teacher upload only; students never need it.

The student app uses `PVZ_SERVER_URL` and enters only the student token. Admin authentication is deliberately separate from the taskbook teacher password in the `.pvz` document.

## API

- `GET /health`: liveness.
- `GET /admin`: browser dashboard (admin key is entered in-memory and is not persisted).
- `POST /api/admin/taskbooks`: admin Bearer key, `application/octet-stream` `.pvz`; responds with one-time token and timestamps.
- `GET /api/admin/taskbooks`: admin Bearer key; returns token hints and lifecycle state.
- `DELETE /api/admin/taskbooks/{id}`: admin Bearer key; revokes the token identified by its dashboard ID.
- `GET /api/taskbooks/{token}`: downloads an active package using its student token.

Packages remain compressed and SHA-256 checked, not encrypted. HTTPS protects network transport; package encryption needs a separate key-management protocol and is not implemented.

## Integration test

From the repository root, run `dotnet build PVZ.sln -c Debug`, then `dotnet run --project PVZ.Server.Tests\PVZ.Server.Tests.csproj -c Debug`. The test starts a local server with temporary data and a random admin key, then checks admin authentication, upload validation, token-based download, persistence across server restart, revocation, and expiry.
