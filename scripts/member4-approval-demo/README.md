# Member 4 approval demo fixtures

`Member4ApprovalDemo` creates five clearly labeled synthetic workflows in a disposable local PostgreSQL database. It refuses remote hosts, ports other than `55432`, or database names outside `agriassist_member4_demo_*`.

The three pending cases are for a person to approve, reject, or request revision through the UI. The other cases show missing upstream evidence and a scheduling conflict. The runner never approves a workflow or creates final workflow tasks, irrigation schedules, or reservations. Its upstream outputs and candidate are synthetic test evidence, not farm recommendations.

Apply the API migrations to a new database before running the fixture project. Supply `ConnectionStrings__DefaultConnection` only for that database. The runner prints workflow IDs and a one-time demo officer login. Use a separate API instance and React dev server pointed at this database; never aim the runner at a live or shared database.

The runner checks that the connection targets `localhost:55432` or `127.0.0.1:55432` and a database whose name starts with `agriassist_member4_demo_`. It does not create the database or apply migrations. Each run adds a new set of five cases, so start with a new disposable database when repeating the demo.

After the first run, pass one exact case name as an argument (for example, `Request revision`) to add just that case. This reuses the existing synthetic officer and keeps its password unchanged. The one-case mode requires exactly one `member4-officer-*@example.test` Agricultural Officer in the demo database.

Expected results:

| Case | Before officer action | What to check |
| --- | --- | --- |
| Approve candidate | Pending Officer Approval | Candidate and validation are visible; approval creates final work once. |
| Reject candidate | Pending Officer Approval | Rejection records a decision and creates no final work. |
| Request revision | Pending Officer Approval | Revision records a decision; stale candidate cannot be approved. |
| Missing weather evidence | Missing Dependency | Blocking validation is visible and approval is unavailable. |
| Conflicting task | Failed | Scheduling conflict is visible and approval is unavailable. |

The mobile application is the Farmer view. It shows approved tasks and irrigation schedules after officer approval; the officer decision controls are in the React console.
