# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A scaffold for the **Risk Register** feature specified in [`SPEC.md`](./SPEC.md). Read `SPEC.md`
before changing anything — it is the requirements document, and `README.md` records the decisions
and open questions taken against it.

**The API is complete** — `GET /api/risks` and `POST /api/risks`, with controller, service,
repository, DTOs, error handling and unit tests, on top of the `dbo.Risks` schema and the
`InitialCreate` migration. The scaffold endpoint `GET /api/hello` has been removed and `Program.cs`
is composition root only.

**The register list is built** — `frontend/src/risks/RisksPage.tsx` at `/risks`, with loading, error
and empty states. `App.tsx` is now the app shell (header, nav, route table) and the dead
`/api/hello` fetch is gone with it.

Still missing: the **status filter control** and the **capture form**, both of which `SPEC.md` asks
for. `status` is already rendered per risk, so the filter is additive.

## Commands

Backend (from `backend/`, serves http://localhost:5080):

```bash
dotnet run --project src/RiskRegister.Api
dotnet build
```

Frontend (from `frontend/`, serves http://localhost:5173):

```bash
npm install
npm run dev
npm run build      # tsc -b && vite build — type errors fail the build
npm run lint       # oxlint
```

Database — required by the API at startup now, since `AddInfrastructure` resolves the connection
string eagerly:

```bash
cp .env.example .env      # then set MSSQL_SA_PASSWORD
docker compose up -d mssql
docker compose ps         # wait for (healthy)
```

The connection string in `appsettings.Development.json` carries a **placeholder password on
purpose**. Set the real one locally, once:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:RiskRegister" \
  "Server=localhost,1433;Database=RiskRegister;User Id=sa;Password=<from .env>;TrustServerCertificate=True" \
  --project src/RiskRegister.Api
```

Migrations (`dotnet-ef` is a **local** tool in `backend/.config/dotnet-tools.json` — `dotnet tool
restore` first on a fresh clone; there is no global install):

```bash
dotnet ef database update    -p src/RiskRegister.Infrastructure -s src/RiskRegister.Api
dotnet ef migrations add Foo -p src/RiskRegister.Infrastructure -s src/RiskRegister.Api -o Migrations
dotnet ef migrations script  -p src/RiskRegister.Infrastructure -s src/RiskRegister.Api
```

Backend tests (`backend/tests/RiskRegister.Tests`, xunit — no database required):

```bash
dotnet test
```

There is **no frontend test runner** installed. `Program.cs` already ends with
`public partial class Program;`, so `WebApplicationFactory<Program>` will work when integration
tests are added.

## Architecture

**Single origin, no CORS.** The browser only ever talks to `localhost:5173`. Vite proxies `/api` to
`http://localhost:5080` (`frontend/vite.config.ts`). There is deliberately no CORS configuration
anywhere and there must not be — this mirrors a reverse proxy in production. Consequently all
frontend API paths are relative (`/api/...`), never absolute.

**Controllers, services, repositories.** The Risk Register uses the standard three-layer pattern
across three projects:

```
RiskRegister.Api  ->  RiskRegister.Infrastructure  ->  RiskRegister.Core
```

`Core` holds the entity, enums, `RiskScoring` and the **interfaces** `IRiskRepository` /
`IRiskService`; it references no NuGet packages, which is what lets the scoring rules be tested with
no host and no database. `Infrastructure` holds the `DbContext`, entity configuration, migrations and
the repository implementations. `Api` holds controllers and DTOs. Nothing points back into `Api`, and
the service layer never sees a `DbContext`.

`Program.cs` maps no endpoints of its own — every endpoint is a controller action.

**Validation shape comes free from `[ApiController]`.** It returns RFC 7807
`ValidationProblemDetails` — `{ "errors": { "title": ["..."] } }` — which is the field-mappable
response `SPEC.md` demands, from plain DataAnnotations on `CreateRiskRequest`. No endpoint filter and
no FluentValidation. An invalid body never reaches the action method, which is why controller unit
tests cannot see one and `CreateRiskRequestTests` validates the DTO directly instead.

**Those error keys are camelCase only because `Program.cs` sets `DictionaryKeyPolicy`.**
`ValidationProblemDetails.Errors` is keyed by CLR property name, so without it a request sent with
`"title"` comes back complaining about `"Title"` and the frontend's field lookup misses. It is not
covered by a test — it is a serialisation setting, and the unit tests never serialise.

**`CreateRiskRequest` trims in its `init` accessors, before validation runs.** Trimming afterwards
would let `"  ab  "` pass the 3-character minimum and then fail `CK_Risks_Title`, turning a 400 into
a 503. A whitespace-only title trims to empty and is reported as *missing*; a blank `description`
normalises to `null`. `likelihood`/`impact` bind as `int?` so that "absent" is distinguishable from
`0` and so `300` is a range error rather than a `byte` overflow during deserialisation; the
controller narrows to `byte` after validation.

**One error shape across the whole API.** `AddProblemDetails()` + `UseExceptionHandler()` in
`Program.cs` give unhandled failures an RFC 7807 body matching the validation one.
`DatabaseExceptionHandler` maps `SqlException` / `DbUpdateException` / `TimeoutException` to **503**
("retry later") instead of a generic 500, and logs the real exception while returning none of it —
connection strings and server names appear in `SqlException` messages.

**Enums are serialised as names.** `Program.cs` registers `JsonStringEnumConverter`, so `status` and
`severity` cross the wire as `"Open"` / `"Critical"`, not `0` / `3`. The frontend keys its severity
styling off those names, so removing the converter silently breaks the UI rather than the build.

**Query-string enums are parsed by `RiskStatusParser`, not by the model binder.** Binding
`RiskStatus?` directly would reject a typo with "The value 'Nonsense' is not valid.", which never
says what *is* valid. Controllers bind `string?` and call `RiskStatusParser.TryParse`, which is a
pure function in Core and therefore unit-testable without a host. **`Enum.TryParse` alone is not
enough** — it returns `true` for `"2"` (→ `Accepted`) and for `"99"` (→ an undefined enum value that
would reach SQL). The parser rejects non-alphabetic input and re-checks `Enum.IsDefined`;
`RiskStatusParserTests` pins that behaviour so it does not get "simplified" away.

**The typed API boundary.** `frontend/src/api/types.ts` mirrors the C# response records one-for-one
in camelCase (ASP.NET Core's default JSON naming policy). When a backend record changes, update the
matching interface — `SPEC.md` requires no `any` at the boundary. `frontend/src/api/client.ts` wraps
`fetch`, throws `ApiError` for both network failures and non-2xx responses, and re-throws
`AbortError` untouched so callers can cancel.

**Request state shape.** `RisksPage.tsx` models its fetch as a discriminated union
(`loading | error | success`) rather than nullable fields, with an `AbortController` cleanup in the
effect. The capture form should follow the same pattern.

**`App.tsx` is the app shell, not a page.** It owns the header, the nav and the route table only;
`<main>` belongs to the routed page, one per document. Screens live in `frontend/src/risks/`.

**The severity band → CSS class map is a `Record<Severity, string>`**, module-local in
`RisksPage.tsx`. A `` `sev-${severity.toLowerCase()}` `` template would be shorter but would let a
new backend band render unstyled; the `Record` makes it a build error instead. It is also why
`Severity` is a string-literal union rather than a loose `string`. The map and the state type stay
unexported so oxlint's `react/only-export-components` does not fire on a `.tsx` file.

**The list is rendered in the order the API returned it.** Ordering is `Score DESC, CreatedUtc DESC,
Id DESC`, done in SQL and carried by two covering indexes — sorting again in the client would only
risk disagreeing with it.

## Constraints that bite

- **Target framework is `net9.0`, pinned by hand.** The `webapi` template only emits `net10.0`, so
  `TargetFramework` and the `Microsoft.AspNetCore.OpenApi` version are set manually in
  `RiskRegister.Api.csproj`. Do not let tooling bump them.
  The `classlib`/`xunit` templates have the same problem — every project's `TargetFramework` was
  edited by hand after `dotnet new`.
- **HTTP only in development.** No `UseHttpsRedirection`, no dev certificate — intentional for
  containers/Codespaces. TLS terminates at the proxy in production.
- **Routing is react-router **v8**, and everything imports from `react-router`.** There is no
  `react-router-dom` in v8 — that package is frozen at 7.18.2 and installing it gets you a shim a
  major version behind. `BrowserRouter`, `Routes`, `Route`, `NavLink` and `Navigate` all come from
  `react-router`; DOM-only APIs live at `react-router/dom`.
- **A hard reload of `/risks` works because Vite's `appType` defaults to `'spa'`.** The HTML fallback
  serves `index.html` for unknown paths, and the `/api` proxy middleware runs ahead of it. Setting
  `appType` to anything else, or deploying behind a proxy with no SPA fallback, 404s every deep link.
- **`apiFetch` does not read the response body on a failure.** A non-2xx throws `ApiError` carrying
  only a message and a status, so the RFC 7807 `errors` map is unreachable today. The list screen does
  not need it; the capture form does, and that is the one place `apiFetch` has to grow. Related: 503
  is reported as "could not reach the API" because Vite's proxy and `DatabaseExceptionHandler` both
  use it, and from the browser the two are indistinguishable.
- **`Risk.Score` is written by SQL Server, never by C#.** It is a persisted computed column, so the
  property has a private setter and EF marks it `ValueGeneratedOnAddOrUpdate`. Assigning to it in
  application code is meaningless, and `INSERT`ing it in raw SQL fails with error 271.
- **`ISNULL` in the `Score` computed column is load-bearing.** SQL Server treats a computed column as
  nullable unless the expression is provably non-null, and does *not* infer that from the operands
  being `NOT NULL`. Without the wrapper the column is nullable while the CLR property is `int`. The
  `CONVERT(INT, ...)` casts matter too: SQL Server does not widen `TINYINT` arithmetic, so
  `TINYINT * TINYINT` stays `TINYINT` and overflows above 255.
- **Ad-hoc SQL against `dbo.Risks` needs `QUOTED_IDENTIFIER ON`.** SQL Server refuses writes to a
  table with an index on a computed column otherwise, and `sqlcmd` defaults it *off* — pass `-I`.
  `SqlClient` sets it on, so EF and the app are unaffected.

## The `dbo.Risks` schema

Defined by `RiskConfiguration.cs`, not by hand-written DDL. The shape:

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int IDENTITY` | clustered PK |
| `Title` | `nvarchar(200)` | `CK_Risks_Title`: `LEN BETWEEN 3 AND 200` |
| `Description` | `nvarchar(2000)` | nullable |
| `Owner` | `nvarchar(100)` | `CK_Risks_Owner`: `LEN BETWEEN 1 AND 100` |
| `Likelihood`, `Impact` | `tinyint` | `BETWEEN 1 AND 5` |
| `Score` | `int` | **persisted computed**, `NOT NULL` |
| `Status` | `nvarchar(20)` | enum **name**, `DEFAULT N'Open'`, `CHECK IN (...)` |
| `CreatedUtc` | `datetimeoffset(3)` | `DEFAULT SYSUTCDATETIME()`, `CHECK TZOFFSET = 0` |

`Severity` is **not a column**. It is derived from `Score` by `RiskScoring.SeverityFor`, so the band
boundaries live in exactly one place — the one the tests cover.

`Status` is stored as the enum *name*, so `RiskStatus`'s member names are part of the database
contract and cannot be renamed without a migration. In exchange, rows read as `Open`/`Closed` in
ad-hoc SQL and the stored value is byte-identical to what the API accepts.

Two indexes, `IX_Risks_Score` and `IX_Risks_Status_Score`, both carry the register's full ordering
(`Score DESC, CreatedUtc DESC, Id DESC`) so the sort is free.

## Decisions already made

- `GET /api/risks?status=Nonsense` returns **400** with the same structured error shape as `POST`,
  not an empty array — `[]` is indistinguishable from "no matching risks" and hides typos.
- **`score` is stored, `severity` is derived.** Ordering happens in SQL, so `Score` has to be a real
  indexable column; a computed one can never drift from its inputs. `Severity` is a pure function of
  it, so storing it would only add a second place to be wrong.
- **New risks default to `Open`**, both in the entity initialiser and as a database default.
  `POST` accepts no `status`, no `score` and no `createdUtc` — all three are the database's, read
  back off the INSERT's OUTPUT clause, so accepting them would let a caller submit values the API
  discards.
- **`POST` returns `Location: /api/risks`**, not `/api/risks/{id}`. There is no GET-by-id endpoint,
  and a `Location` header that 404s is worse than one that resolves; the created risk is in the body
  either way.
- **`RiskService.CreateAsync` re-checks the axis range and the blank title/owner** and throws
  `ArgumentOutOfRangeException` / `ArgumentException`. A backstop against a programming error
  elsewhere reaching a CHECK constraint as a 503 — not the validation, which the DTO already did.
- **Ordering tie-break is `Score DESC, CreatedUtc DESC, Id DESC`.** `SPEC.md` orders by score alone,
  which is non-deterministic — 3×4 and 4×3 both score 12.
- **The validation error shape is RFC 7807 `ValidationProblemDetails`**, per `[ApiController]`.
- **`?status=open` is valid** — status parsing is case-insensitive, while stored and returned values
  stay canonical (`"Open"`). Query strings get hand-typed; rejecting a casing difference gains
  nothing. `?status=` (blank) means no filter; `?status=2` is a 400, since the query string names a
  status rather than carrying the enum's storage value.
- Remaining unresolved questions from `SPEC.md` (whether frontend tests are required) are listed at
  the end of `README.md`.

## Testing

`backend/tests/RiskRegister.Tests` is **pure unit tests — no database, no `WebApplicationFactory`**.
`FakeRiskRepository` stands in for EF and records what it was asked for.

Two things worth knowing before adding tests here:

- **`FakeRiskRepository` sets `Score` by reflection.** The property has a private setter because SQL
  Server computes it, so no test can populate it the normal way. The reflection is confined to one
  private helper rather than loosening the entity for tests. `AddAsync` assigns `Id`, `Score` and
  `CreatedUtc` for the same reason the real repository gets them from the OUTPUT clause — a fake that
  left them at zero would let a controller drop them and still pass.
- **`RiskRepository` itself is not covered.** Its ordering happens in SQL, and neither EF InMemory
  nor SQLite can evaluate a persisted computed column — `Score` comes back `0` on both, so a green
  test would be actively misleading. The `ORDER BY` and computed `Score` are verified manually
  against the real container instead (see `backend/scripts/seed-risks.sql`). Closing this properly
  needs an integration test against SQL Server.
