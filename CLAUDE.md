# EWMS — Employee Workflow Management System

منصة داخلية متعددة الفروع: الهيكل (فرع ← قسم ← مكتب ← مستخدم)، الأدوار والصلاحيات، الإجازات، المهام، الصيانة وقطع الغيار، توثيق الأجهزة والخريطة، التقارير، والمشاريع وجداولها.
(Internal multi-branch platform; **vacations are only one feature** — dashboards show organization-wide info, not vacation numbers.)

Org hierarchy: **Branch → Department → Office → User**. `Vacation` approval scoping deliberately stays at Department/Branch level only — it does NOT use Office (confirmed with the user 2026-09-27).
Code comments and user-facing messages are in Arabic; keep that convention.

> **هذا الملف فهرس قصير.** التفاصيل التشغيلية لكل وحدة في [`docs/architecture/`](docs/architecture/README.md)، و**أسباب** القرارات في [`docs/adr/`](docs/adr/README.md). اقرأ الملف المعني قبل تعديل وحدته.

## Stack
- Backend: ASP.NET Core **.NET 10**, EF Core 10 + SQL Server (LocalDB, DB name `EWMS`), MediatR 12, FluentValidation 12, AutoMapper 16, JWT Bearer, BCrypt.
- Frontend: **Angular 22** standalone components. **Active UI is `F:\-EWMS-UI-main-main`** (separate folder, chosen by the user 2026-09-27; its own git repo `yammann/EWMS-FrontEnd`, work branch `yaman`). `EWMS.Client/` from older notes does not exist here. The UI's own reference is `docs/ARCHITECTURE.md` there; its structure and design are summarised in [`docs/architecture/frontend.md`](docs/architecture/frontend.md). Checks: `npm run lint`, `npm run check:cycles`, `ng test`, `npm run e2e`.
- Solution file: `EWMS.slnx` (API, Application, Domain, Infrastructure + `tests/`). `EWMS/Class1.cs` is an empty leftover, not in the solution.

## Architecture (Clean Architecture + CQRS) — [ADR-0001](docs/adr/0001-clean-architecture-cqrs.md)
Dependencies: `API → Application, Infrastructure`; `Infrastructure → Application, Domain`; `Application → Domain`. Application never uses EF Core.

- **Domain/** — entities and enums. `User.BranchId/DepartmentId/OfficeId` are nullable (SuperAdmin has none); `Office.DepartmentId` is required.
- **Application/** — `Features/<Feature>/` with **one handler per use case** ([ADR-0010](docs/adr/0010-handler-per-use-case.md)): `UseCases/<Name>.cs` = request record + validator + handler; a `<Feature>Context` (registered in `AddApplicationRegistrationServices.cs`) holds the shared services and load/guard code; a static `<Feature>Support` holds shared pure helpers. Older features still use the single-file multi-handler style (declared debt in `LayeringTests`) — match the file you're editing and split it when you next change that module. Also `DTOs/Request|Response`, `Helper/Profiles` (AutoMapper), `Interfaces/` (`IXxxService`), `Common/` (rules, `AppPermissions`, `Viewer`), `Common/Behaviors/ValidationBehavior`.
- **Infrastructure/** — `Persistence/Data/DataContext.cs` (mostly `DeleteBehavior.Restrict`), `DbSeeder.cs`, `Migrations/`, `Persistence/Repositories/*Service.cs` implement the Application interfaces, `AddInfrastructureRigstrationServices.cs` (DI + JWT).
- **API/** — `SystemBuild/Program.cs` (auto-runs migrations + seeding on startup), `AddAPIRigstrationServices.cs` (Swagger + one authorization policy per permission), `Controllers/` (thin: only `_mediator.Send(...)`), `Middlewares/ExceptionMiddleware.cs`: `KeyNotFoundException`→404, `ArgumentException`/`InvalidOperationException`→400, `UnauthorizedAccessException`→401 (403 when authenticated), `ValidationException`→400 with errors list. **Handlers signal errors by throwing these.**

Request flow: Controller → MediatR → ValidationBehavior → Handler → `IXxxService` → EF Core.

### Adding a new feature
1. Entity in `Domain/Entities`, DbSet + config in `DataContext`, migration (`dotnet ef migrations add <Name> -p Infrastructure -s API`). A required FK on a populated table: hand-edit the migration to backfill first. Any new parent→child relationship needs a friendly **delete guard** (a clear `InvalidOperationException`, not a raw FK 500).
2. Interface in `Application/Interfaces`, implementation in `Infrastructure/Persistence/Repositories`, register in Infrastructure DI.
3. Under `Application/Features/<Feature>/`: `UseCases/` (one file per use case), a `<Feature>Context`, DTOs + AutoMapper profile.
4. Controller in `API/Controllers` with `[Authorize(Policy = "…")]`. New permission → add it to `Application/Common/AppPermissions.cs` **only** (one per operation: `ViewXs`/`CreateX`/`EditX`/`DeleteX`, Arabic description stating its fixed record boundary); the policy, DB row and SuperAdmin grant are generated. Mirror it in the UI `core/constants/access.ts` and the roles-page tree, and refresh the API contract snapshots (`UPDATE_API_CONTRACT=1 dotnet test`, then `contract:sync` + `api:types` in the UI — [ADR-0016](docs/adr/0016-api-contract-snapshots.md)).
5. Tests: unit tests for the pure rules, an integration test for the main path and boundaries ([quality-gates](docs/architecture/quality-gates.md)). Update the matrix with `UPDATE_PERMISSION_MATRIX=1` and review the diff.

## Non-negotiable rules (each has an ADR — read it before bending one)
- **Access = role + permission only; each permission has a fixed record boundary coded in the handler.** No scopes, no inference from a role's name or the user's placement ([ADR-0002](docs/adr/0002-role-permission-only-access.md), [0003](docs/adr/0003-one-permission-per-operation.md)). Handlers read permissions through `Viewer.CurrentAsync`; a user with no unit matches no unit (`null == null` must not pass).
- **The current user and permissions come from the database, never from token claims**; role/placement/password/deactivation changes revoke sessions ([ADR-0004](docs/adr/0004-current-user-from-database.md)).
- **The system must work offline**: no CDN, map library, tile layer, external font or script ([ADR-0005](docs/adr/0005-offline-first.md)).
- **History is append-only**: signatures are versioned, decisions store the version they used, costs are frozen at issue, audit is written in the same transaction ([ADR-0007](docs/adr/0007-immutable-history.md)). **The one who prepares does not approve** ([ADR-0008](docs/adr/0008-separation-of-duties.md)).
- **Never put `HasDefaultValue` on a bool or enum property** (EF omits `false`, so the DB writes the default; an architecture test enforces it).
- **Secrets only from configuration/environment**; device passwords are AES-GCM encrypted and never leave the server in a list or DTO ([ADR-0013](docs/adr/0013-secrets-and-encryption.md)).
- Rejected options are listed in [ADR-0015](docs/adr/0015-rejected-options.md) — don't re-propose them unasked.

## Where the details are
| Topic | File |
|-------|------|
| Auth, JWT, lockout, security headers, the permission model and boundaries | [auth-and-permissions](docs/architecture/auth-and-permissions.md) |
| Vacation workflow, working days & payment, printing, attachments, «إجازاتي», electronic signatures | [vacations-and-signatures](docs/architecture/vacations-and-signatures.md) |
| Attachment storage (database or SMB folder) | [file-storage](docs/architecture/file-storage.md) |
| Running locally, configuration, release packages | [running-and-release](docs/architecture/running-and-release.md), `docs/DEPLOYMENT.md` |
| Offices, user placement, notifications and SignalR | [organization-and-notifications](docs/architecture/organization-and-notifications.md) |
| Angular wiring, structure, visual identity, pagination and shared UI | [frontend](docs/architecture/frontend.md) |
| Work tasks and dashboards | [work-tasks-and-dashboards](docs/architecture/work-tasks-and-dashboards.md) |
| Assigned-task board (review, templates, recurrence, stats) | [task-board](docs/architecture/task-board.md) |
| Device inventory (sites, devices, installations) | [device-inventory](docs/architecture/device-inventory.md) |
| Syria map and branch layers | [maps-and-layers](docs/architecture/maps-and-layers.md) |
| Maintenance, spare parts | [maintenance](docs/architecture/maintenance.md) |
| «مفكرتي» personal lists | [todo-lists](docs/architecture/todo-lists.md) |
| Reports center | [reports-center](docs/architecture/reports-center.md) |
| Projects, documents, schedule/delay/extension (phases 0–1) | [projects](docs/architecture/projects.md) |
| Diwan: incoming/outgoing correspondence, referrals (phase 2, steps 2.1–2.4 done) | [diwan](docs/architecture/diwan.md) |
| Studies & technical offices: study orders, per-office deliverables, sending to the center (phase 3, step 3.1 done) | [studies](docs/architecture/studies.md) |
| CI, test layers, permission matrix, regression tests, handler structure | [quality-gates](docs/architecture/quality-gates.md) |

## Running and testing (short)
- API: `dotnet run --project API` → https://localhost:7181 (Swagger `/swagger`), http://localhost:5204. Startup stops with an Arabic list if configuration is missing. Details: [running-and-release](docs/architecture/running-and-release.md).
- UI: `cd F:\-EWMS-UI-main-main && npm start` → http://localhost:4200 (restart after any `proxy.conf.json` change).
- Tests: `dotnet test EWMS.slnx -c Release` (**Release**, because a running `API.exe` locks `API\bin\Debug`). Integration tests need Docker running. Definition of done for a feature: unit tests for its rules, an integration test for its main path and boundaries, a live run with a real account, docs updated, CI green.

## Known issues / tech debt (not fixed yet)
1. (Angular client sync — resolved, see [frontend](docs/architecture/frontend.md).)
2. (Fixed 2026-09-29, migration `Fix_Vacation_Bool_Defaults`) `Vacation.IsPaid` / `BranchManagerAccept` had `HasDefaultValue(true)`: EF omits a `false` bool from INSERT, so the DB wrote `true` — unpaid vacations were saved as paid. **Never put `HasDefaultValue` on a bool or enum property** (the enum case — `Vacation.Status` default `PendingManager`, EF warning 20601 — was removed 2026-10-06 by migration `Vacation_Status_Remove_Default`; adding an `Unspecified = 0` member alone does not silence it, a sentinel or dropping the default does).
3. Project leftovers: `Stage` (has `ProjectId`), `Tasks`, `EmpReport`, empty `StageTasks`/`State`, `API/wwwroot/uploads/projects/` (from the removed first "Projects" feature, commit `ba670e2`; unrelated to the current Projects module).
4. Secrets (resolved 2026-10-28): the tracked `appsettings.json` has no secrets, dev keys are in `appsettings.Development.json`, and production refuses any key published in the repo ([ADR-0013](docs/adr/0013-secrets-and-encryption.md)). Server action when first deploying this change: generate new keys and rotate the device key as in `docs/DEPLOYMENT.md`.
5. Some namespaces are inconsistent (`...Vacations.Query.GetAll` vs `...Queries.GetByUser`; folder `Department/Command` vs `Branches/Commands`); match the file you're editing.
6. Declared architecture debt, enforced by explicit lists in the tests so it can only shrink: `AuthController` takes `IAuthService` and `VacationsController` takes `IUserService` (`ApiContractTests.ThinControllerDebt`); 28 older handlers serve several requests (`LayeringTests.LegacyMultiRequestHandlers`).
- **`dotnet ef database update` always targets the real `EWMS` database**: `Infrastructure/Persistence/Data/DataContextFactory` hard-codes the connection string and ignores `ConnectionStrings__DefaultConnection` (the API honors it, and auto-migrates at startup). To test migrations on a scratch DB, run the API with the env var set, never `ef database update` (or pass nothing and accept it hits EWMS).

## Conventions
- Main branch `master`; check `git branch` for the current work branch (currently `north`). Ask before pushing; merge PRs with merge commits.
- Keep controllers thin; business logic lives in Handlers; data access in Infrastructure services.
- Error messages to users in Arabic; code comments in Arabic.
- A decision that touches permissions, data, security or layering gets an ADR in `docs/adr/` before merge; module details live in `docs/architecture/` — update them with the code, not in this file.
