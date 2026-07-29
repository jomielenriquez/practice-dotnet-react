# Risk Register

Scaffold for the Risk Register described in [`SPEC.md`](./SPEC.md).

**Current state: schema and data layer built, no endpoints yet.** `GET /api/hello` still proves the
stack is wired end to end. The `dbo.Risks` table, the `Risk` entity and the `InitialCreate` migration
exist and are verified against SQL Server; the endpoints and the UI are not — see
[Not built yet](#not-built-yet).

| Part | Stack | Location |
| --- | --- | --- |
| Backend | ASP.NET Core 9 Web API, controller / service / repository | `backend/` |
| Frontend | React 19 + TypeScript + Vite | `frontend/` |
| Database | SQL Server 2022 in Docker, EF Core 9 | `docker-compose.yml` |

```
backend/src/
  RiskRegister.Core/            entity, enums, scoring rules, IRiskRepository, IRiskService
  RiskRegister.Infrastructure/  DbContext, entity configuration, migrations   -> Core
  RiskRegister.Api/             controllers, DTOs, composition root           -> Core, Infrastructure
backend/tests/
  RiskRegister.Tests/           xunit
```

## Prerequisites

- .NET SDK 9 or later (this repo builds under SDK 10 targeting `net9.0`)
- Node.js 20+
- Docker with Compose v2 (only needed once the database is in play)

## Running it

The database must be up first — the API resolves its connection string at startup. See
[Database](#database), then two terminals.

**Terminal 1 — backend** (http://localhost:5080)

```bash
cd backend
dotnet tool restore          # first time only: dotnet-ef is a local tool
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

```bash
cp .env.example .env      # then change the password
docker compose up -d mssql
docker compose ps         # wait for (healthy)
```

The connection string in `appsettings.Development.json` has a **placeholder password on purpose** —
the real one is never committed. Set it once, matching the password in `.env`:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:RiskRegister" \
  "Server=localhost,1433;Database=RiskRegister;User Id=sa;Password=<from .env>;TrustServerCertificate=True" \
  --project src/RiskRegister.Api

dotnet ef database update -p src/RiskRegister.Infrastructure -s src/RiskRegister.Api
```

`TrustServerCertificate=True` is required: SQL Server 2022 encrypts by default with a self-signed
certificate generated on first boot, which nothing local trusts.

It listens on `localhost:1433` with user `sa`. Data persists in the `mssql-data` volume;
`docker compose down -v` wipes it. **The SA password is baked into that volume on first boot** —
changing `.env` afterwards makes the container fail its healthcheck with "Login failed for user
'sa'". Recreating the volume is the fix.

Poking at the table by hand needs `sqlcmd -I`: `QUOTED_IDENTIFIER` must be on to write to a table
with an index on a computed column, and `sqlcmd` defaults it off. EF is unaffected.

### The schema

`dbo.Risks`, defined by `RiskConfiguration.cs` rather than hand-written DDL:

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int IDENTITY` | clustered PK |
| `Title` | `nvarchar(200)` | `CHECK LEN BETWEEN 3 AND 200` |
| `Description` | `nvarchar(2000)` | nullable |
| `Owner` | `nvarchar(100)` | `CHECK LEN BETWEEN 1 AND 100` |
| `Likelihood` | `tinyint` | `CHECK BETWEEN 1 AND 5` |
| `Impact` | `tinyint` | `CHECK BETWEEN 1 AND 5` |
| `Score` | `int` | persisted computed: `likelihood × impact`, `NOT NULL` |
| `Status` | `nvarchar(20)` | `DEFAULT N'Open'`, `CHECK IN ('Open','Mitigating','Accepted','Closed')` |
| `CreatedUtc` | `datetimeoffset(3)` | `DEFAULT SYSUTCDATETIME()`, `CHECK TZOFFSET = 0` |

**`Score` is computed and persisted, `Severity` is not stored at all.** The register orders by score,
which has to happen in SQL, so `Score` must be a real indexable column — and a computed one can never
disagree with `likelihood × impact`. `Severity` is a pure function of `Score`, so storing it would
only create a second place for the band boundaries to be wrong; it lives once, in
`RiskScoring.SeverityFor`, which is what the boundary test covers.

**`Status` stores the enum name, not its number.** Rows read as `Open`/`Closed` in ad-hoc SQL and the
stored value is byte-identical to what the API accepts and returns. A `tinyint` would leave the
meaning only in C#, where reordering the enum silently corrupts existing rows. The cost is that the
`RiskStatus` member names are now part of the database contract.

**`datetimeoffset`, not `datetime2`.** It round-trips to `DateTimeOffset` and serialises with an
explicit `+00:00`; `datetime2` comes back as a `DateTime` with `Kind=Unspecified` and reaches the
frontend with no zone marker.

The CHECK constraints duplicate the API's validation rules deliberately. The API validates first and
returns field-mapped errors; these are the backstop so no code path can write a row `SPEC.md`
forbids. `nvarchar(200)` alone cannot express a *minimum* length.

Both indexes carry the register's full ordering — `Score DESC, CreatedUtc DESC, Id DESC` — so the
sort is free:

```
IX_Risks_Score         (Score DESC, CreatedUtc DESC, Id DESC)
IX_Risks_Status_Score  (Status, Score DESC, CreatedUtc DESC, Id DESC)
```

## Notable choices

**Controller / service / repository across three projects.** `Api → Infrastructure → Core`, with
nothing pointing back into `Api`. The split is what makes the pattern mean anything: the repository
*interfaces* live in `Core` next to the service that consumes them, the EF implementations live in
`Infrastructure`, so the service layer cannot reach a `DbContext`. `Core` also references no NuGet
packages, which is what lets the scoring rules be tested with no host and no database.

A side benefit: `[ApiController]` returns RFC 7807 `ValidationProblemDetails` —
`{ "errors": { "title": ["..."] } }` — automatically from DataAnnotations. That is exactly the
field-mappable error shape `SPEC.md` asks for, with no endpoint filter and no FluentValidation, which
minimal APIs on `net9.0` would have needed.

`GET /api/hello` is still a minimal API in `Program.cs`; it is scaffolding, not part of the feature.

**SQL Server in Docker, not a local install.** Reproducible, disposable, and version-pinned per
repo. The connection string will come from configuration, so pointing at a local or Azure SQL
instance later means changing one setting, not the code.

**HTTP only in development.** No HTTPS redirection and no dev certificate — those are friction in a
container or Codespace and buy nothing locally. TLS terminates at the proxy in production.

**`net9.0`, not `net8.0`.** `SPEC.md` says .NET 8; this targets .NET 9 by decision. The `webapi`
template only emits `net10.0`, so the target framework and the `Microsoft.AspNetCore.OpenApi`
version are pinned by hand in `backend/src/RiskRegister.Api/RiskRegister.Api.csproj`.

## Built

- The `dbo.Risks` schema, the `Risk` entity, `RiskScoring`, and the `InitialCreate` migration,
  verified against SQL Server 2022 — every CHECK constraint proven to fire, `Score` proven
  non-writable and `NOT NULL`, ordering and tie-break proven.
- `RiskRegister.Tests` with the score → severity boundary mapping covered (16 tests).

## Not built yet

- `GET /api/risks`, `POST /api/risks`, and the `RiskService` / `RiskRepository` implementations
  behind them — the interfaces exist, the implementations do not.
- The register list and the capture form.
- The `Risk` and `ValidationProblemDetails` interfaces in `frontend/src/api/types.ts`.
- Any test of a validation failure (the second `SPEC.md` quality criterion). `Program.cs` already
  ends with `public partial class Program;`, so `WebApplicationFactory<Program>` will work.
- No frontend test runner is installed.

### Questions in `SPEC.md`, settled by the schema

- **Stored vs derived `score`/`severity`** — `score` is a persisted computed column, `severity` is
  derived in C# and never stored. Reasoning under [The schema](#the-schema).
- **Status on create** — `POST` takes no `status`, so new risks are `Open`, both in the entity
  initialiser and as a database default.
- **Ordering tie-break** — `Score DESC, CreatedUtc DESC, Id DESC`. Score alone is non-deterministic:
  3×4 and 4×3 both score 12. Both indexes carry the full ordering.
- **Validation error shape** — RFC 7807 `ValidationProblemDetails`, which `[ApiController]` produces
  from DataAnnotations. Field-mappable by construction; camelCase keys matching the request DTO.
- **`createdDate` is server-assigned UTC** — named `CreatedUtc`, defaulted by `SYSUTCDATETIME()`, and
  a CHECK constraint enforces a zero offset rather than trusting callers.
- **`GET /api/risks?status=Nonsense` returns 400** with that same error shape. Returning `[]` is
  indistinguishable from "no Open risks" and silently hides typos.

### Still open

- Is `?status=open` valid, or only `Open`? The stored values are `Open`-cased; whether the query
  string is parsed case-insensitively is still a decision.
- No auth, pagination or tenancy mentioned — assumed out of scope.
- "At least one test" ×2 — unclear whether frontend tests are required at all.
