# Northwind Support Desk — assessment implementation

.NET 10 API, React 19/TypeScript and SQL Server. Requirements: [ASSIGNMENT_CANDIDATE.md](ASSIGNMENT_CANDIDATE.md). Detailed reasoning: [APPROACH.md](APPROACH.md).

## Delivery status

| Task | Status | Rough active AI-assisted time* |
|---|---|---|
| 1: defect and SQL filters | Implemented and tested; separate commits | 15 minutes |
| 2: triage, escalation, SLA | Implemented and tested; migration included | 25 minutes |
| 3: authentication | Implemented and tested; migration included | 15 minutes |
| 4: Docker | Implemented; **live container verification unfinished** | 15 minutes |

*Approximate implementation/testing time, not the candidate's personal effort. Excludes overnight pauses, account setup and download waits. Candidate review/rehearsal is still needed. Commits are preserved locally, but publishing to `uni4zahri/northwind-support` is blocked by the GitHub integration's HTTP 403 write-permission error. Do not submit the unchanged remote as completed work.

## Run locally

Requires Node 22+, .NET SDK 10 and Docker Desktop (Linux containers). From the repository root:

```sh
npm ci
npm run setup                 # creates ignored .env and a random JWT key
npm run db:up
npm run api                   # terminal 1; reads .env, runs HTTP profile
npm run web                   # terminal 2
```

Allow SQL Server to become healthy before starting the API. Web: http://localhost:5173; native API: http://localhost:5043; Swagger: http://localhost:5043/swagger. In Development startup applies migrations and seeds demo data. Existing development databases also receive missing password hashes. `npm run setup` preserves an existing signing key. Never commit `.env`.

**Development login:** `alex.turner@northwind-support.example` / `Northwind_Local_Only_2026!`. All seeded active agents share this deliberately local fixture password; inactive Yuki cannot sign in. This is not a production credential.

For Docker backend, after `npm run setup`:

```sh
docker compose up --build
# another terminal, POSIX shell:
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

PowerShell equivalent: `$env:VITE_API_PROXY_TARGET='http://localhost:5080'; npm run web`. API: http://localhost:5080; health: http://localhost:5080/health. Compose injects the connection string using service name `sqlserver`, signing key, environment, token settings and SLA settings. Overrides are listed in `.env.example`. Native API SLA settings use `Sla` in `appsettings.Development.json`, or equivalent `Sla__...` environment variables. Restart after configuration changes. Missing/short JWT keys fail startup clearly.

## Design decisions and behaviour

- **DEFECT-117:** the effect watched search/page/page size but omitted other filters. Debounce search alone; use the complete serialized query as the dependency. Ignore obsolete responses. Rejected manual dependency enumeration because a new filter could reintroduce the defect. The regression test fails on the original code and passes on the fix.
- **Query composition:** exact matches and trimmed title/reference/customer search become SQL `WHERE` clauses before count, sort and page. Filters combine with AND; named-agent plus unassigned-only returns zero. Case sensitivity follows database collation. Add another predicate in `ApplyFilters`; the SLA addition follows that pattern. SQLite tests execute relational filtering; a SQL Server `ToQueryString` test verifies SLA `CASE`/`DATEADD`/`DATEDIFF` translation. Live SQL Server execution has not been tested here.
- **Pure policy + orchestration:** `TriagePolicy` shares priority/window/eligibility logic between create and escalation; `TicketTriage` handles database access. Rejected rules in controllers or duplicated create/escalate logic. Category flags control forced Critical priority and specialist requirements. Fewest open tickets wins; lowest agent ID breaks ties. No eligible agent yields a normal unassigned ticket with a reason.
- **Capacity assumption:** exclude the current ticket when assessing an existing owner's capacity. Escalation raises exactly one level and restarts the window from the injected clock; Critical/Resolved/Closed reject with 409. A stored `SlaStartedAtUtc` preserves the restarted window; old seed rows use creation time. SLA status is derived, including inclusive at-risk and on-time-resolution boundaries.
- **Persistence/unit of work:** escalation and history save together. History has private setters, no editing endpoint and an async save guard. Its `(TicketId, EscalatedAtUtc)` index supports per-ticket newest-first history; agent FK indexes support referential checks. Restrict deletion preserves references. New migrations add history/SLA start and a nullable password-hash column; no SLA status column.
- **Authentication:** Agent owns its credential because this app has one account type; rejected a redundant Users table. Identity PasswordHasher provides salted PBKDF2. JWTs use HS256, configured key/issuer/audience, 60-minute default expiry and zero clock skew. Central fallback authorization protects controllers; login, Development Swagger and health are open. History actor comes only from token `sub` (stable agent ID), never request input.
- **Browser session:** `sessionStorage` survives refresh within a tab; logout/401 clears it and protected routes redirect to login, preserving the destination. It is accessible to JavaScript, so XSS remains a risk. Production should consider secure HttpOnly cookies/BFF, HTTPS and a reviewed session lifecycle.

## Verification and Docker limits

```sh
npm test
npm run typecheck
npm run lint
npm run build
```

Verified: 48 backend tests (44 behavioural/unit + 4 architecture/model), 13 frontend tests, lint/type checks, React production build and API Release publish. Tests cover priority/tier/floor, specialists/capacity/ties, owner retention/unassignment, escalation history/rejections, SLA boundaries, filtered paging, credential hashing, generic login failures, JWT protection, browser 401 and validation. API tests use TestServer and SQLite; no running database is required. For restricted build environments: `dotnet test --disable-build-servers -m:1 -p:UseSharedCompilation=false`.

Dockerfile uses cached project-first restore, SDK build and ASP.NET-only runtime, non-root `$APP_UID`, HTTP 8080, and excludes `.env`, development settings, web files and build output. Compose binds API port 5080 to localhost and waits for SQL health. `/health` reports database reachability (200/503) without authentication; it is tested through TestServer.

**No Docker executable/daemon was available.** YAML parsing and Release publish passed; the following remain unverified and Task 4 is not fully finished:

| Required check | Run on a Docker-equipped machine |
|---|---|
| Empty-volume migrations/seed | `docker compose down -v` (deletes local demo data), then `docker compose up --build`; check `/health`, log in and fetch `/api/tickets` |
| Restore cache | Change one `.cs` file; `docker compose build --progress=plain api`; confirm restore is CACHED |
| Non-root user | `docker compose exec api whoami` (must not be root) |
| Runtime vs SDK image size | `docker image inspect northwind-support-api:local mcr.microsoft.com/dotnet/sdk:10.0 --format '{{.RepoTags}} {{.Size}}'`; figures **not measured** |

Production migrations should run once as a controlled deployment step, with a migration identity; application replicas should not race to migrate or have schema privileges. Demo seeding is Development-only.

## AI use and known limitations

Codex assisted implementation, tests and documentation. Accepted composable filters; rejected a hard-coded hook dependency list. Verification caught an AI-generated JSX typo, missing binder dependency and a test tracking conflict, all corrected. Regression failure-before/pass-after and relational/HTTP tests validated generated work. See APPROACH for walkthrough guidance.

Concurrent triage can overbook an agent; concurrent escalation lacks optimistic locking. At scale use transactional allocation/concurrency tokens, indexes and server-side candidate selection. History guards do not protect against direct DBA edits. Substring search is not a full-text index; SLA filtering may scan. No token revocation, timing equalization for missing accounts, registration/reset, roles, OAuth, refresh tokens, MFA, notifications, CI, caching or stretch report. These are deliberately outside scope. Live SQL Server, Docker startup, cache and image-size verification remain outstanding.
