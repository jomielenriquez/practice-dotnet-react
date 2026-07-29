# Risk Register

Scaffold for the Risk Register described in [`SPEC.md`](./SPEC.md).

**Current state: scaffold only.** There is one endpoint, `GET /api/hello`, which the React app
calls to prove the stack is wired end to end. None of the Risk Register feature is implemented yet —
see [Not built yet](#not-built-yet).

| Part | Stack | Location |
| --- | --- | --- |
| Backend | ASP.NET Core 9 Web API, minimal APIs | `backend/` |
| Frontend | React 19 + TypeScript + Vite | `frontend/` |
| Database | SQL Server 2022 in Docker | `docker-compose.yml` |

## Prerequisites

- .NET SDK 9 or later (this repo builds under SDK 10 targeting `net9.0`)
- Node.js 20+
- Docker with Compose v2 (only needed once the database is in play)

## Running it

Two terminals.

**Terminal 1 — backend** (http://localhost:5080)

```bash
cd backend
dotnet run --project src/RiskRegister.Api
```

**Terminal 2 — frontend** (http://localhost:5173)

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173. You should see the greeting fetched from the API.

The frontend calls the relative path `/api/hello`; Vite's dev server proxies `/api` to
`http://localhost:5080` (see `frontend/vite.config.ts`). Because the browser only ever talks to one
origin there is **no CORS configuration anywhere** — which is also how a reverse proxy would work in
production.

To check the API directly:

```bash
curl -s http://localhost:5080/api/hello
# {"message":"Hello from the Risk Register API","utcNow":"2026-07-29T..."}
```

The OpenAPI document is served at http://localhost:5080/openapi/v1.json in Development.

### Database

Not yet used by the application — start it only when you need it.

```bash
cp .env.example .env      # then change the password
docker compose up -d mssql
docker compose ps         # wait for (healthy)
```

It listens on `localhost:1433` with user `sa`. Data persists in the `mssql-data` volume;
`docker compose down -v` wipes it.

## Notable choices

**Minimal APIs, not controllers.** The surface is one endpoint today and two once the register
lands. Controllers add a base class, attribute routing and MVC conventions that pay off across
dozens of endpoints, not two. `TypedResults` also makes each response type part of the handler
signature, which keeps the generated OpenAPI document honest and feeds the typed frontend client.

**SQL Server in Docker, not a local install.** Reproducible, disposable, and version-pinned per
repo. The connection string will come from configuration, so pointing at a local or Azure SQL
instance later means changing one setting, not the code.

**HTTP only in development.** No HTTPS redirection and no dev certificate — those are friction in a
container or Codespace and buy nothing locally. TLS terminates at the proxy in production.

**`net9.0`, not `net8.0`.** `SPEC.md` says .NET 8; this targets .NET 9 by decision. The `webapi`
template only emits `net10.0`, so the target framework and the `Microsoft.AspNetCore.OpenApi`
version are pinned by hand in `backend/src/RiskRegister.Api/RiskRegister.Api.csproj`.

## Not built yet

- The whole Risk Register feature: `GET /api/risks`, `POST /api/risks`, the register list, the
  capture form.
- **EF Core and any database access.** When it lands: add `Microsoft.EntityFrameworkCore.SqlServer`
  `9.0.*` and a `dotnet-ef` *local* tool manifest (`dotnet-ef` is not installed on this machine).
  The connection string will need `TrustServerCertificate=True`, since SQL Server 2022 encrypts by
  default with a self-signed certificate.
- **Tests.** No test project. When one is added, `Program.cs` needs `public partial class Program;`
  for `WebApplicationFactory<Program>` to work.
- **Validation.** Minimal APIs on `net9.0` have no built-in DataAnnotations validation — that
  arrived in .NET 10. `POST /api/risks` will need an endpoint filter or FluentValidation to produce
  the field-mappable error shape `SPEC.md` requires.

### Open questions in `SPEC.md`

Raised during planning, still unresolved. None of them block this scaffold:

- `POST /api/risks` takes no `status`, but `status` is a required field of a risk — default to `Open`?
- Ordering by score descending has no tie-breaker (3×4 and 4×3 both score 12), so results are
  non-deterministic as specified.
- Are `score` and `severity` stored or derived? They must be computed server-side, but ordering
  happens in SQL.
- The exact JSON shape and key casing of the validation error response — the frontend has to match
  it exactly.
- Is `?status=open` valid, or only `Open`?
- `createdDate` is presumably server-assigned UTC; never stated.
- No auth, pagination or tenancy mentioned — assumed out of scope.
- "At least one test" ×2 — unclear whether frontend tests are required at all.

Decided: `GET /api/risks?status=Nonsense` returns **400** with the same structured error shape as
`POST`. Returning `[]` is indistinguishable from "no Open risks" and silently hides typos.
