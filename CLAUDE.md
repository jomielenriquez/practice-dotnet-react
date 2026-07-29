# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A scaffold for the **Risk Register** feature specified in [`SPEC.md`](./SPEC.md). Read `SPEC.md`
before changing anything — it is the requirements document, and `README.md` records the decisions
and open questions taken against it.

Currently the only endpoint is `GET /api/hello`, which the React app calls to prove the stack is
wired end to end. None of the Risk Register feature (`GET /api/risks`, `POST /api/risks`, the
register list, the capture form) exists yet.

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

Database (not yet used by the app; start only when needed):

```bash
cp .env.example .env      # then set MSSQL_SA_PASSWORD
docker compose up -d mssql
docker compose ps         # wait for (healthy)
```

There is **no test project yet** on either side, so there is no test command. When adding one:

- Backend: `Program.cs` needs `public partial class Program;` appended before
  `WebApplicationFactory<Program>` will work.
- Frontend: no test runner is installed.

## Architecture

**Single origin, no CORS.** The browser only ever talks to `localhost:5173`. Vite proxies `/api` to
`http://localhost:5080` (`frontend/vite.config.ts`). There is deliberately no CORS configuration
anywhere and there must not be — this mirrors a reverse proxy in production. Consequently all
frontend API paths are relative (`/api/...`), never absolute.

**Minimal APIs, not controllers.** Endpoints are mapped directly in
`backend/src/RiskRegister.Api/Program.cs` using `TypedResults`, which keeps response types in the
handler signature and the generated OpenAPI document honest. Keep new endpoints in this style.

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
- **No built-in validation on net9.0 minimal APIs.** DataAnnotations validation for minimal APIs
  arrived in .NET 10. `POST /api/risks` needs an endpoint filter or FluentValidation to produce the
  field-mappable error shape `SPEC.md` demands.
- **HTTP only in development.** No `UseHttpsRedirection`, no dev certificate — intentional for
  containers/Codespaces. TLS terminates at the proxy in production.
- **No EF Core yet.** When added: `Microsoft.EntityFrameworkCore.SqlServer` `9.0.*`, a *local*
  `dotnet-ef` tool manifest (`dotnet-ef` is not installed on this machine), and
  `TrustServerCertificate=True` in the connection string — SQL Server 2022 encrypts by default with
  a self-signed certificate.

## Decisions already made

- `GET /api/risks?status=Nonsense` returns **400** with the same structured error shape as `POST`,
  not an empty array — `[]` is indistinguishable from "no matching risks" and hides typos.
- Remaining unresolved questions from `SPEC.md` (status default on create, score tie-breaking,
  stored vs derived `score`/`severity`, error-response JSON shape, status casing) are listed at the
  end of `README.md`. Resolve them there when they get answered.
