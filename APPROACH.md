# Implementation decisions

## Filtering and the defect

The list effect originally watched search and pagination but omitted the other filters. A regression test asserted the outgoing status request and failed before the fix. The complete serialized query now drives the effect; search alone is debounced and late responses are ignored. This avoids maintaining a second list of filter dependencies.

Backend filters compose as `IQueryable.Where` predicates before count, sort and paging. All combine with AND; whitespace-only search is ignored. Case sensitivity follows database collation. SQLite tests check actual relational results, SQL Server translation tests check provider-specific SLA expressions, and the live browser suite checks combined filters against SQL Server.

## Business rules and persistence

A pure policy implements priority, SLA-window and agent-eligibility decisions. An infrastructure service handles queries and persistence. This keeps rules reusable between create and escalation without placing database work in the domain or duplicating logic in controllers.

Agent ID breaks equal-workload ties. The ticket being escalated is excluded when checking its current owner's capacity. This makes retention possible without counting the same slot twice. If no agent qualifies, creation still succeeds unassigned with a reason.

Escalation needs a persisted SLA start because it resets the response window. Later assignment/status changes must not reset that start. Legacy records use creation time. Status remains derived using an injected clock, so it cannot become stale in storage.

Ticket and escalation history save in the same unit of work. Private setters, no editing endpoint and an EF guard prevent ordinary application updates/deletes to history; these do not protect against direct database administration. Restricted foreign-key deletion preserves references.

## Authentication

Agent holds the password hash because the application has one account type. Identity PasswordHasher is used instead of custom hashing. Existing development agents receive missing hashes during seeding; inactive accounts cannot sign in.

JWTs validate signature, algorithm, issuer, audience and expiry. Escalation attribution uses the numeric token subject. Central authorization avoids accidentally exposing a newly added controller. Browser sessionStorage provides tab-scoped refresh persistence, but XSS can access the token. HttpOnly cookies/BFF and a reviewed session lifecycle would be production alternatives.

## Containers and validation

The Dockerfile copies project and central configuration files before source so ordinary source changes reuse restore. Published output runs in the ASP.NET image as the base image's non-root user. Environment variables provide deployment configuration; the ignored .env is generated locally.

Docker checks covered an empty test volume, restored original volume, SQL-backed health, non-root identity, image size and cache reuse. [FINAL_VERIFICATION.md](FINAL_VERIFICATION.md) records the later live browser run. The 48-test backend suite and 13-test frontend suite provide broader rule coverage than the browser smoke checks.

In Codespaces, legacy iptables rules initially dropped traffic across the project bridge despite working DNS and a healthy SQL container. A rule limited to incoming/outgoing traffic on that bridge restored connectivity. This is environment-specific; inspect rules before changing them and reassess after a network restart. Later Docker content-store/snapshot errors required build-cache regeneration and container recreation while preserving named database volumes. Neither issue required an application-rule change.

Development Data Protection key-storage warnings remain in logs. Production needs explicit key persistence/encryption if features relying on those keys are used. Production migrations should run once with a deployment identity rather than giving each application replica schema privileges.

## AI workflow and review

Development followed an AI-assisted collaborative workflow. Codex generated implementation and tests and supported troubleshooting and documentation. The candidate ran validation commands in Codespaces and reviewed the results with AI assistance. Composable query predicates were accepted; a manually maintained hook dependency list was rejected because new filters could recreate the original defect. Verification identified and corrected a generated JSX typo, missing binder dependency and test tracking conflict. Generated code was checked with regression failure-before/pass-after, relational and HTTP tests, builds, Docker checks and real Chromium flows.

## Limits and scaling

Concurrent allocation/escalation still needs concurrency control. For large ticket volumes, review execution plans, add workload-appropriate indexes/full-text search and calculate candidate workloads in SQL instead of loading all agents. Security work beyond the assessment includes hardened sessions, revocation and generic-failure timing. No claim is made of production readiness or exhaustive cross-browser/accessibility testing.

## Time accounting

The candidate's approximate retrospective estimate is 30 hours overall: Task 1 5 hours, Task 2 11 hours, Task 3 6 hours and Task 4 8 hours. This includes implementation, learning, review, testing, documentation and environment troubleshooting, as reflected in the submission email. Individual task times were not precisely tracked. These overall effort estimates supersede the earlier narrow AI-assisted implementation estimates.

The recorded 3.2-minute successful verification run is a measured execution duration within that workflow, not an additional personal-effort estimate.
