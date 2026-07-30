# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A scaffold for the **Risk Register** feature specified in [`SPEC.md`](./SPEC.md). Read `SPEC.md`
before changing anything — it is the requirements document, and `README.md` records the decisions
and open questions taken against it.

**`GET /api/risks` is built** — controller, service, repository, DTO, error handling and unit tests —
on top of the `dbo.Risks` schema and the `InitialCreate` migration. `GET /api/hello` remains as the
scaffold check. Still missing: `POST /api/risks` (`RiskService.CreateAsync` and
`RiskRepository.AddAsync` both throw `NotImplementedException`), the register list and the capture
form.

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

The legacy `GET /api/hello` is still a minimal API in `Program.cs`; new endpoints are controllers.

**Validation shape comes free from `[ApiController]`.** It returns RFC 7807
`ValidationProblemDetails` — `{ "errors": { "title": ["..."] } }` — which is the field-mappable
response `SPEC.md` demands, from plain DataAnnotations on the request DTO. No endpoint filter and no
FluentValidation.

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

**Request state shape.** `App.tsx` models fetches as a discriminated union
(`loading | error | success`) rather than nullable fields, with an `AbortController` cleanup in the
effect. The register list is expected to follow the same pattern.

## Constraints that bite

- **Target framework is `net9.0`, pinned by hand.** The `webapi` template only emits `net10.0`, so
  `TargetFramework` and the `Microsoft.AspNetCore.OpenApi` version are set manually in
  `RiskRegister.Api.csproj`. Do not let tooling bump them.
  The `classlib`/`xunit` templates have the same problem — every project's `TargetFramework` was
  edited by hand after `dotnet new`.
- **HTTP only in development.** No `UseHttpsRedirection`, no dev certificate — intentional for
  containers/Codespaces. TLS terminates at the proxy in production.
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

- **`FakeRiskRepository.Create` sets `Score` by reflection.** The property has a private setter
  because SQL Server computes it, so no test can populate it the normal way. The reflection is
  confined to that one factory rather than loosening the entity for tests.
- **`RiskRepository` itself is not covered.** Its ordering happens in SQL, and neither EF InMemory
  nor SQLite can evaluate a persisted computed column — `Score` comes back `0` on both, so a green
  test would be actively misleading. The `ORDER BY` and computed `Score` are verified manually
  against the real container instead (see `backend/scripts/seed-risks.sql`). Closing this properly
  needs an integration test against SQL Server.
