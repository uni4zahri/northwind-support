# Final automated verification

Completed: 2026-10-17T07:13:38.667588+00:00. Runner elapsed time: 3.2 minutes (includes installation/download waits; not personal effort).

Playwright 1.64.1; Chromium 156.0.8078.4; real React UI and live SQL-backed API in Codespaces. No mocked API responses.

- npm ci and npm audit: passed; zero reported vulnerabilities at run time.
- Frontend tests, typecheck, lint, and combined backend/frontend build: passed.
- Protected route redirects to login; browser login opens ticket list
- Browser creates ticket with default Medium priority, future SLA and visible assignment explanation
- Browser search and combined status/priority/category/customer/SLA filters update both SQL results and displayed rows; unassigned filter checked
- Browser escalation raises Medium to High, changes due time and persists history across reload
- Logout protects direct detail navigation; anonymous API remains 401; no uncaught browser errors

Development test ticket retained: `TCK-1003`. Each rerun creates one labelled ticket and one escalation. Existing tickets and volumes are not deleted.

These are automated Chromium smoke checks, not a claim of manual visual, accessibility, cross-browser or exhaustive business-rule testing. Earlier backend tests and Docker measurements are documented in README and APPROACH. Candidate understanding/interview preparation cannot be verified by this script.
