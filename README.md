# Northwind Support Desk

.NET 10 Web API, React 19/TypeScript and SQL Server. Implements all four tasks in [the assessment](ASSIGNMENT_CANDIDATE.md), with separate commits for the filter defect and database filtering.

## Run the application

Requirements: Node matching [.nvmrc](.nvmrc), Docker with Compose, and .NET SDK 10 for native API development or backend tests. GitHub Codespaces can run the Docker backend without installing Docker on your physical computer.

```sh
npm ci
npm run setup                   # creates ignored .env; generates/preserves JWT signing key
docker compose up --build -d
# In another terminal:
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

Open http://localhost:5173. API: http://localhost:5080; Swagger: http://localhost:5080/swagger; health: http://localhost:5080/health. In Codespaces, forward port 5173 and use `VITE_API_PROXY_TARGET=http://localhost:5080 npm run dev --workspace apps/web -- --host 0.0.0.0`. PowerShell: `$env:VITE_API_PROXY_TARGET='http://localhost:5080'; npm run web`.

**Development login:** `alex.turner@northwind-support.example` / `Northwind_Local_Only_2026!`. Active seeded agents share this local fixture password; inactive agents cannot sign in. Development startup applies migrations and seeds demo data.

For native API development, start SQL with `npm run db:up`, wait for database health, then run `npm run api` and `npm run web` in separate terminals. The native API uses port 5043, matching the default frontend proxy.

Configuration is documented in [.env.example](.env.example): database password, JWT signing key/issuer/audience/lifetime, environment and SLA settings. Compose supplies the database host as `sqlserver`. Never commit `.env`. Missing or short signing keys stop startup. Native SLA configuration uses the existing `Sla` section or equivalent `Sla__...` environment variables.

## Implementation and decisions

- **Task 1 — defect and filtering.** DEFECT-117 came from missing filter dependencies in the list effect. Only search is debounced; the complete serialized query drives requests, and obsolete responses are ignored. A regression test failed on the original hook and passed after the fix. A manually maintained dependency list was rejected because new filters could repeat the defect. Exact matches and trimmed title/reference/customer search combine with AND in SQL before count, ordering and paging. A new filter adds a predicate in `ApplyFilters`; no client-side filtering is used. SQLite tests execute relational queries; SQL Server translation tests inspect SLA SQL, and live browser checks exercise combined filters.
- **Task 2 — triage and escalation.** A pure `TriagePolicy` holds shared rules; `TicketTriage` orchestrates persistence. Rules in controllers or duplicate create/escalate logic were rejected. Requested priority defaults to Medium; category flags force Critical or require specialists. Default windows are Low 72h, Medium 24h, High 8h and Critical 4h; Premium halves the window with a 1h floor. Settings are configurable. Eligible active agents below capacity are ordered by open workload, then agent ID. No eligible agent produces an unassigned ticket with a reason. Escalation raises priority once, restarts the SLA window and keeps an eligible owner or reassigns. Critical/Resolved/Closed tickets reject escalation. SLA state is derived using an injected clock, including the inclusive at-risk threshold.
- **Database and assumptions.** Migrations add escalation history, nullable credential hashes and `SlaStartedAtUtc`; SLA status is not stored. Ticket and history changes save atomically. History has private setters, no edit endpoint and an EF update/delete guard. The `(TicketId, EscalatedAtUtc)` index supports newest-first ticket history; agent FK indexes support referential checks, with restricted deletion. An owner's capacity excludes the ticket being escalated. Legacy SLA windows fall back to creation time; search case sensitivity follows database collation. Named-agent plus unassigned-only filters intentionally return no matches.
- **Task 3 — authentication.** Credentials stay on Agent because there is one account type; a separate Users table adds no useful distinction. Identity PasswordHasher provides salted PBKDF2. HS256 JWTs validate key, issuer, audience and expiry; lifetime defaults to 60 minutes with zero clock skew. Central fallback authorization protects controllers; login, Development Swagger and health are open. Escalation actor comes from token `sub`, never the request body. The browser stores the session in `sessionStorage`; logout/401 clears it. This survives tab refresh but remains exposed to XSS.
- **Task 4 — Docker.** Project-first restore enables caching; the SDK stage publishes into an ASP.NET runtime image running as non-root on port 8080. Development settings and secrets are excluded from the build context. Compose publishes API port 5080 on localhost and waits for SQL health. Production should use a separate, controlled migration step and restricted runtime database privileges, avoiding startup migration races and demo seeding.

See [APPROACH.md](APPROACH.md) for trade-offs, scaling considerations and troubleshooting.

## Verification

```sh
npm test
npm run typecheck
npm run lint
npm run build
npm audit
```

Verified: **48 backend tests** (44 behavioural/unit and 4 architecture/model), **13 frontend tests**, lint, type checking, frontend production build and API publish. Coverage includes SLA boundaries/windows, eligibility/capacity/ties, escalation/history/rejections, relational filtering, real JWT middleware, hashing, browser authentication and validation. Automated API tests use TestServer/SQLite; they are distinct from live SQL Server checks. The source-map-js update to 1.2.2 was validated with zero npm audit findings at verification time.

| Docker check | Recorded result |
|---|---|
| Empty-volume startup | Separate new named volume; migrations/seed completed, 40 tickets seeded and health HTTP 200. Original volume restored afterward. |
| Restore cache | Temporary C# comment reran source copy/publish; restore remained CACHED. Original source restored. |
| Non-root identity | `uid=1654(app) gid=1654(app)` |
| Image size | API 455,430,070 bytes; SDK 1,303,841,725 bytes — approximately 65.1% smaller. Measured before the later recovery rebuild. |
| API authentication | Anonymous tickets 401; login, current-user lookup and authenticated tickets 200. |

[FINAL_VERIFICATION.md](FINAL_VERIFICATION.md) records successful live Chromium checks for creation, combined filters, escalation/history and logout. To rerun in Codespaces: `python3 scripts/final-check/finish.py`. It installs temporary browser tooling, creates one labelled demo ticket and updates the verification report; it does not commit or push. The inherited CI workflow has no recorded runs at this review; the results above were verified outside GitHub Actions.

## AI use and limitations

Development followed an AI-assisted collaborative workflow. Codex supported implementation, test creation, troubleshooting and documentation across all four tasks; the candidate ran checks in Codespaces and reviewed their outputs with AI assistance. The composable filtering approach was accepted; a manually enumerated dependency list was rejected. Generated JSX, a missing configuration-binder dependency and a test tracking conflict were corrected. Verification used the failing/passing regression, relational and HTTP tests, builds and live Docker/browser checks.

Concurrent triage can overbook agents; concurrent escalation lacks optimistic locking. Production needs transactional allocation/concurrency tokens, reviewed session handling and appropriate indexes. History guards cannot prevent direct DBA edits; substring search and derived SLA filters may scan. Token revocation, timing equalization for unknown accounts and other production authentication features are not implemented. Registration/reset, roles, OAuth, refresh tokens, MFA, notifications and the optional SLA report are outside scope. Browser coverage is a Chromium smoke suite, not exhaustive visual, accessibility or cross-browser testing.

## Approximate time spent

Approximately **30 hours overall**, estimated retrospectively across implementation, learning, review, testing, documentation and environment troubleshooting. The task allocation below is approximate, not a precisely tracked timesheet.

| Task | Approximate effort |
|---|---:|
| Task 1 — Filter defect and server-side filtering | 5 hours |
| Task 2 — Triage, escalation, history and SLA handling | 11 hours |
| Task 3 — Authentication and protected routes | 6 hours |
| Task 4 — Docker setup, troubleshooting and verification | 8 hours |
| **Total** | **30 hours** |

These candidate-reported overall effort estimates replace the earlier narrow AI-assisted implementation estimates and match the submission email. Automated execution durations are separate measurements within the workflow, not additional effort to add to this total: the final successful verification runner recorded 3.2 minutes, including installation waits.
