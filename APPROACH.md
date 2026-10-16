# Implementation approach and work log

## Task 1 — defect and filtering

The list hook watched search and pagination but omitted the remaining filters. I first added a test that asserted the outgoing status request and observed it fail on the original hook. The fix debounces search alone, uses a complete serialized query as the effect dependency, and ignores late results from obsolete requests. This makes new scalar filters participate automatically without maintaining another dependency list.

Filtering is composed as `IQueryable.Where` predicates before `CountAsync`, ordering and paging. Filters combine with AND, including the deliberately contradictory combination of a named agent and unassigned-only (zero results). Blank search is ignored; other search values are trimmed. Case sensitivity follows database collation. A real SQLite relational test verifies priority, combined predicates, customer-name search and filtered paging. SQL Server-specific SLA translation is checked separately.

Validation before Task 2: 31 backend tests and 3 frontend tests passed; frontend lint and type checking passed. The regression test failed before the fix. Defect and filtering are separate commits.

## Task 2 — triage, escalation and SLA

Use a pure `TriagePolicy` for priority, SLA windows and deterministic agent selection, plus an infrastructure service for EF queries and persistence. The existing command handler retains responsibility for creation. Category flags drive rules; category names never do.

Escalation excludes the ticket itself when checking current-owner capacity: keeping a ticket does not consume an additional slot. Agent ID breaks workload ties. No eligible agent is a normal unassigned outcome. SLA start is stored because escalation restarts the response window and later assignment/status updates must not move that start. Legacy seed tickets fall back to their creation timestamp. SLA status itself is derived. History uses private setters, no mutation endpoint, and an EF save guard against update/delete; ticket and history save atomically. Database administrators can still edit data directly.

A reusable SLA indicator already existed; extend it rather than inventing a second component. Escalation refreshes the ticket and history without a browser reload and preserves existing ticket data after a conflict.

## Environment and publication

Work is in a local checkout whose origin is `https://github.com/uni4zahri/northwind-support.git`. The upstream repository is not a push target. GitHub connector reads succeed, but tree creation returned HTTP 403 "Resource not accessible by integration" on both attempts. Local history is preserved; no claim is made that these commits have been published.

The original container had no .NET SDK or Docker. Installed .NET 10.0.402 to run tests. A session restart removed the temporary SDK; restored it under the working directory. Used single-process MSBuild and disabled compiler servers to avoid the environment's named-pipe limitation. No Docker daemon is available yet.

## AI usage

Codex generated implementation and tests, inspected source and test output, and made corrections. Accepted the small composable filtering approach. Rejected manually enumerating hook dependencies because adding SLA would otherwise repeat the original bug. Verification caught a missing JSX closing brace in generated triage markup and a missing configuration-binder package; both were corrected. Do not claim this was independently handwritten by the candidate: review the implementation and rehearse the trade-offs before submission.

## Time accounting

Task 1 involved approximately 10–15 minutes of active AI-assisted work across the initial sessions. Final estimates for all tasks appear below. These are rough observed working-time estimates, not a claim about the candidate's personal effort; connection waits, overnight pauses and environment restoration are excluded.

## Task 3 — authentication

Credentials live on Agent because the assignment has a single account type and excludes registration, roles and external identity. A separate users table adds no useful distinction here. Store only Identity PasswordHasher output (salted PBKDF2); never store the fixture password as a database value. Seed missing hashes on existing development databases as well as fresh ones. Inactive agents cannot log in. Email matching trims whitespace and ignores case; failures share one message.

JWT validation checks signature, algorithm, issuer, audience and expiration with zero clock skew. A fallback policy protects every controller except explicitly anonymous login; Swagger remains open in Development. Escalation actor is the stable numeric agent ID from `sub`, so renaming an agent does not corrupt history attribution and supplied actor fields have no effect. Tokens last 60 minutes. Session storage provides tab-scoped persistence; logout and 401 clear it. A server token revocation mechanism and hardened cookie-based browser sessions would be production considerations, outside this assessment.

## Task 4 — container and developer experience

Project files and central build properties are copied before source to preserve the restore cache. Publish in the SDK stage and copy only published output into ASP.NET runtime; use the base image's non-root APP_UID and port 8080. Exclude Development settings from the build context so the inherited local database password is not carried in the image. Compose supplies environment-specific settings and waits for SQL Server health. The anonymous health endpoint checks connectivity without leaking connection details.

The Node setup helper generates an ignored local JWT key and preserves it on later runs. The native API wrapper loads .env explicitly; ASP.NET Core does not load dotenv files by itself. Native development defaults to HTTP 5043, matching the Vite proxy, while the Docker workflow selects 5080 explicitly.

Docker execution could not be completed: neither the executable nor a daemon/socket exists in this environment. Static YAML validation, Release publish and HTTP health endpoint tests passed. Image size, runtime UID, clean-volume boot and cached rebuild are explicitly not measured. README contains the commands to finish those checks; optional Task 4 remains incompletely verified.

## Final verification and review topics

The backend suite reached 48 passing tests, including HTTP tests of real JWT middleware and password verification; frontend reached 13 passing tests plus lint, type checking and build. The original transitive Pkcs 9.0.11 dependency emitted vulnerability warnings; a central version override upgrades it to 10.0.13. No actual SQL Server instance or container was used; do not confuse SQL translation/SQLite tests with production-provider integration tests.

Walk through: (1) how the list effect reacts without debouncing dropdowns, (2) why query composition occurs before counting, (3) pure policy versus persistence orchestration, (4) why escalation needs its own SLA start, (5) immutable history and atomic save, (6) central JWT validation and token-derived actor, (7) XSS/sessionStorage and concurrency limits. For 10 million tickets, investigate execution plans/full-text search, indexed assignment workload and SQL-side candidate selection; avoid loading all agents. For production, add concurrency control, reviewed authentication/session handling and controlled schema deployment rather than claiming this local demo is production-ready.

Final rough active AI-assisted time: Task 1 ~15 min; Task 2 ~25 min; Task 3 ~15 min; Task 4/setup/docs ~15 min. Estimates exclude pauses and access/environment waits and are not the candidate's personal time record.
