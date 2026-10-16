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

Task 1 involved approximately 10–15 minutes of active AI-assisted work across the initial sessions. Task 2 and later tasks are still in progress. These are rough observed working-time estimates, not a claim about the candidate's personal effort; connection waits, overnight pauses and environment restoration are excluded.
