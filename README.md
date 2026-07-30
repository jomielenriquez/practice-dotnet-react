# Risk Register

Scaffold for the Risk Register described in [`SPEC.md`](./SPEC.md).

**Current state: the API is complete, the UI is not.** `GET /api/risks` and `POST /api/risks` are
built and verified against SQL Server, on top of the `dbo.Risks` table, the `Risk` entity and the
`InitialCreate` migration. The register list and the capture form are not — see
[Not built yet](#not-built-yet).

> The scaffold endpoint `GET /api/hello` has been removed. **`frontend/src/App.tsx` still fetches
> it**, so the page currently renders its error state; it is replaced by the register list, which is
> the next piece of work.

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

Open http://localhost:5173. Until the register list lands, the page shows its error state — it still
calls the removed `/api/hello`.

The frontend calls relative paths (`/api/risks`); Vite's dev server proxies `/api` to
`http://localhost:5080` (see `frontend/vite.config.ts`). Because the browser only ever talks to one
origin there is **no CORS configuration anywhere** — which is also how a reverse proxy would work in
production.

To check the API directly:

```bash
curl -s http://localhost:5080/api/risks

curl -s -X POST http://localhost:5080/api/risks \
  -H 'Content-Type: application/json' \
  -d '{"title":"Backups are never restore-tested","owner":"Priya Raman","likelihood":4,"impact":5}'
# {"id":13,...,"score":20,"severity":"Critical","status":"Open","createdUtc":"..."}
```

`backend/src/RiskRegister.Api/RiskRegister.Api.http` and `CreateRisk.http` hold the same requests as
editor-runnable files, including every validation failure.

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

`Program.cs` is now composition root only — configuration, middleware, `MapControllers()`. Every
endpoint is a controller action.

**SQL Server in Docker, not a local install.** Reproducible, disposable, and version-pinned per
repo. The connection string will come from configuration, so pointing at a local or Azure SQL
instance later means changing one setting, not the code.

**HTTP only in development.** No HTTPS redirection and no dev certificate — those are friction in a
container or Codespace and buy nothing locally. TLS terminates at the proxy in production.

**`net9.0`, not `net8.0`.** `SPEC.md` says .NET 8; this targets .NET 9 by decision. The `webapi`
template only emits `net10.0`, so the target framework and the `Microsoft.AspNetCore.OpenApi`
version are pinned by hand in `backend/src/RiskRegister.Api/RiskRegister.Api.csproj`.

## `GET /api/risks`

Returns the register, worst first. Ordering is `Score DESC, CreatedUtc DESC, Id DESC`, matching the
index key order exactly so the sort comes free.

| Request | Response |
| --- | --- |
| `/api/risks` | `200`, every risk |
| `/api/risks?status=Open`, `?status=open`, `?status=OPEN` | `200`, filtered — casing is ignored |
| `/api/risks?status=` (blank) | `200`, treated as no filter |
| `/api/risks?status=Nonsense`, `?status=2` | `400`, `errors.status` names the valid values |
| empty register | `200` with `[]`, never `204` |
| database unreachable | `503` `ProblemDetails` |

```jsonc
// 200
[{ "id": 1, "title": "Customer database has no tested restore path", "description": "...",
   "owner": "Priya Raman", "likelihood": 5, "impact": 5, "score": 25,
   "severity": "Critical", "status": "Open", "createdUtc": "2026-07-01T09:15:00+00:00" }]

// 400 — field-mappable, keyed by the query parameter
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "status": ["'Nonsense' is not a valid status. Valid values are: Open, Mitigating, Accepted, Closed."] } }
```

`severity` and `status` cross the wire as **names, not numbers** — `Program.cs` registers
`JsonStringEnumConverter`. Removing it silently breaks the frontend's severity styling.

`?status=2` is rejected deliberately. `Enum.TryParse` accepts it (`2` → `Accepted`) and accepts `99`
too, yielding an undefined enum value that would reach SQL. `RiskStatusParser` rejects non-alphabetic
input and re-checks `Enum.IsDefined`; the query string names a status, it does not carry the enum's
storage value.

## `POST /api/risks`

Creates a risk and returns it, scored. The request body is exactly the five fields `SPEC.md` lists:

| Field | Rule |
| --- | --- |
| `title` | required, 3–200 characters |
| `description` | optional, max 2000 characters |
| `owner` | required, 1–100 characters |
| `likelihood` | required, integer 1–5 |
| `impact` | required, integer 1–5 |

| Request | Response |
| --- | --- |
| valid body | `201`, the created risk, `Location: /api/risks` |
| invalid body | `400`, `errors` keyed by field, every offending field at once |
| unparseable value (`"likelihood": "high"`) | `400`, keyed by JSON path (`$.likelihood`) |
| database unreachable | `503` `ProblemDetails` |

```jsonc
// request
{ "title": "Customer database has no tested restore path",
  "description": "Nightly backups report success; a restore has never been attempted.",
  "owner": "Priya Raman", "likelihood": 4, "impact": 5 }

// 201 — score and severity are computed server-side, status starts Open
{ "id": 13, "title": "Customer database has no tested restore path", "description": "Nightly ...",
  "owner": "Priya Raman", "likelihood": 4, "impact": 5, "score": 20,
  "severity": "Critical", "status": "Open", "createdUtc": "2026-07-30T10:16:56.528+00:00" }

// 400 — one round trip reports every field, so the form marks all of them up at once
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "title": ["Title must be between 3 and 200 characters."],
              "owner": ["Owner is required."],
              "likelihood": ["Likelihood must be an integer between 1 and 5."] } }
```

**`status`, `score` and `createdUtc` are not accepted.** New risks are always `Open`; `Score` is a
persisted computed column and `CreatedUtc` defaults to `SYSUTCDATETIME()`. All three are read back
off the INSERT's OUTPUT clause, so the response carries the database's values — accepting them on
the way in would only let a caller submit something the API silently discards.

**Text fields are trimmed before validation, not after.** `"  ab  "` is six characters raw and would
pass a 3-character minimum, then hit `CK_Risks_Title` and surface as a **503** — an outage-shaped
response to what is really a 400. Trimming in the DTO's `init` accessor makes validation, the
response and the stored row agree on one value, and a whitespace-only title becomes empty and is
reported as *missing* rather than *too short*. A blank `description` normalises to `null`.

**`likelihood` and `impact` bind as `int?`, not `byte`.** A missing value on a non-nullable value
type binds to `0` and is reported as out of range rather than absent; and `300` overflows a `byte`
during deserialisation, producing a JSON error instead of the range message. The narrowing to `byte`
happens in the controller, after validation.

**Validation error keys are camelCase** because `Program.cs` sets `DictionaryKeyPolicy`.
`ValidationProblemDetails.Errors` is keyed by CLR property name, so without it a body sent with
`"title"` comes back complaining about `"Title"`, and the frontend's field lookup misses.

**`Location` is the register, not `/api/risks/{id}`.** There is no GET-by-id endpoint — `SPEC.md`
does not ask for one — and a `Location` header that 404s is worse than one that resolves. The
created risk is in the body either way, which is what the frontend uses.

The service layer re-checks the axis range and rejects a blank title or owner with
`ArgumentOutOfRangeException` / `ArgumentException`. That is a backstop, not the validation: the DTO
has already returned a field-mapped 400. It exists so a programming error elsewhere in the
application fails loudly instead of arriving at a CHECK constraint as a 503.

### Sample data

`backend/scripts/seed-risks.sql` loads 12 realistic risks spanning all four severity bands and all
four statuses, including a deliberate score-12 tie (3×4 and 4×3) so the tie-break is visible. The
header comment has the command; it needs `sqlcmd -I`.

## Built

- The `dbo.Risks` schema, the `Risk` entity, `RiskScoring`, and the `InitialCreate` migration,
  verified against SQL Server 2022 — every CHECK constraint proven to fire, `Score` proven
  non-writable and `NOT NULL`, ordering and tie-break proven.
- `GET /api/risks` end to end: controller, `RiskService`, `RiskRepository`, `RiskResponse`,
  `RiskStatusParser`, and RFC 7807 error handling including a 503 for database outages.
- `POST /api/risks` end to end: `CreateRiskRequest` with its DataAnnotations, `RiskService.CreateAsync`,
  `RiskRepository.AddAsync`, and a 201 carrying the database-computed `score` and derived `severity`.
  Verified against the real container — trimming, blank-to-null, the field-mapped 400 and the
  store-generated round trip all confirmed by hand as well as by test.
- 104 unit tests: severity boundaries, status parsing, request validation, service and controller
  behaviour.

## Not built yet

- The register list and the capture form. **`frontend/src/App.tsx` still calls the removed
  `/api/hello`** and shows its error state until they land; `HelloResponse` in
  `frontend/src/api/types.ts` no longer mirrors anything.
- The `Risk` and `ValidationProblemDetails` interfaces in `frontend/src/api/types.ts`.
- **Any test of `RiskRepository`.** Its ordering happens in SQL, and neither EF InMemory nor SQLite
  can evaluate a persisted computed column — `Score` comes back `0` on both, so a passing test would
  be misleading. Verified manually against the container instead; an integration test against real
  SQL Server would close it. `Program.cs` already ends with `public partial class Program;`, so
  `WebApplicationFactory<Program>` will work.
- No frontend test runner is installed.

### Questions in `SPEC.md`, settled by the schema

- **Stored vs derived `score`/`severity`** — `score` is a persisted computed column, `severity` is
  derived in C# and never stored. Reasoning under [The schema](#the-schema).
- **Status on create** — `POST` takes no `status`, so new risks are `Open`, both in the entity
  initialiser and as a database default.
- **Ordering tie-break** — `Score DESC, CreatedUtc DESC, Id DESC`. Score alone is non-deterministic:
  3×4 and 4×3 both score 12. Both indexes carry the full ordering.
- **Validation error shape** — RFC 7807 `ValidationProblemDetails`, which `[ApiController]` produces
  from DataAnnotations. Field-mappable by construction; camelCase keys matching the request body,
  which needs `DictionaryKeyPolicy` set — see [`POST /api/risks`](#post-apirisks).
- **`createdDate` is server-assigned UTC** — named `CreatedUtc`, defaulted by `SYSUTCDATETIME()`, and
  a CHECK constraint enforces a zero offset rather than trusting callers.
- **`GET /api/risks?status=Nonsense` returns 400** with that same error shape. Returning `[]` is
  indistinguishable from "no Open risks" and silently hides typos.

- **`?status=open` is valid** — parsing is case-insensitive, responses stay canonical (`"Open"`).
  Query strings get hand-typed and hand-edited; rejecting a casing difference gains nothing, and a
  genuine typo still returns 400.

### Still open

- No auth, pagination or tenancy mentioned — assumed out of scope.
- "At least one test" ×2 — unclear whether frontend tests are required at all.
