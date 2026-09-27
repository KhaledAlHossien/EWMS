# EWMS — Employee Workflow Management System

Internal HR system: branches, departments, offices, users, roles/permissions, and a multi-stage **vacation request workflow**.

Org hierarchy: **Branch → Department → Office → User**. `Vacation` approval scoping deliberately stays at Department/Branch level only — it does NOT use Office (confirmed with the user 2026-09-27).
The old "Projects" feature was removed in commit `ba670e2` and replaced by Vacations.
Code comments and user-facing messages are in Arabic; keep that convention.

## Stack
- Backend: ASP.NET Core **.NET 10**, EF Core 10 + SQL Server (LocalDB, DB name `EWMS`), MediatR 12, FluentValidation 12, AutoMapper 16, JWT Bearer, BCrypt.
- Frontend: **Angular 22** standalone components in `EWMS.Client/` (its own git repo, untracked by the main repo).
  `-EWMS-UI-main/` is another copy of the UI from a different repo — not the active one unless the user says so.
- Solution file: `EWMS.slnx` (API, Application, Domain, Infrastructure). `EWMS/Class1.cs` is an empty leftover, not in the solution.

## Architecture (Clean Architecture + CQRS)
Dependencies: `API → Application, Infrastructure`; `Infrastructure → Application, Domain`; `Application → Domain`.

- **Domain/** — entities (`User`, `Branch`, `Department`, `Office`, `Role`, `Permission`, `RolePermission`, `Vacation`, `VacationType`, `UserToken`) and `Enums/VacationStatus`. `User.OfficeId` is required (belongs to exactly one office); `Office.DepartmentId` is required (belongs to exactly one department).
- **Application/**
  - `Features/<Feature>/Commands|Queries/<Action>/` → `XxxCommand` (IRequest), `XxxCommandHandler`, `XxxCommandValidator`.
  - `DTOs/Request`, `DTOs/Response`, `Helper/Profiles` (AutoMapper), `Interfaces/` (`IXxxService`).
  - `Common/Behaviors/ValidationBehavior` runs all FluentValidation validators in the MediatR pipeline.
  - Registered in `AddApplicationRegistrationServices.cs`.
- **Infrastructure/**
  - `Persistence/Data/DataContext.cs` (relationships, indexes, mostly `DeleteBehavior.Restrict`), `DbSeeder.cs`, `Migrations/`.
  - `Persistence/Repositories/*Service.cs` implement the Application interfaces (they are services/repositories over `DataContext`).
  - `AddInfrastructureRigstrationServices.cs` — DI + JWT config.
- **API/**
  - `SystemBuild/Program.cs` (entry; auto-runs migrations + seeding on startup), `AddAPIRigstrationServices.cs` (Swagger + authorization policies), `ApplicationPipeline.cs`.
  - `Controllers/` — thin, only call `_mediator.Send(...)`.
  - `Middlewares/ExceptionMiddleware.cs` maps exceptions → HTTP: `KeyNotFoundException`→404, `ArgumentException`/`InvalidOperationException`→400, `UnauthorizedAccessException`→401, `ValidationException`→400 with errors list. **Handlers signal errors by throwing these.**

Request flow: Controller → MediatR → ValidationBehavior → Handler → `IXxxService` → EF Core.

### Adding a new feature (follow existing pattern)
1. Entity in `Domain/Entities`, DbSet + config in `DataContext`, add migration (`dotnet ef migrations add <Name> -p Infrastructure -s API`).
2. Interface in `Application/Interfaces`, implementation in `Infrastructure/Persistence/Repositories`, register in Infrastructure DI.
3. DTOs + AutoMapper profile + Commands/Queries/Handlers/Validators under `Application/Features/<Feature>`.
4. Controller in `API/Controllers`; if a new permission is needed: add policy in `AddAPIRigstrationServices.cs` AND the permission in `DbSeeder` (and role mapping).

## Auth & permissions
- `POST api/Auth/login` → JWT with claims: NameIdentifier (UserId), Email, Name, Role, `DepartmentId`, `BranchId`.
- Tokens are stored in `UserTokens`; `OnTokenValidated` rejects revoked/expired tokens, so logout really revokes.
- Current user: `ICurrentUserService` (also `IUserService.UserId`) reads claims.
- Authorization = named policies (`[Authorize(Policy = "ManageUsers")]`) checked by `API/Authorization/PermissionAuthorizationHandler` against `RolePermissions` in the DB.
- Seeded roles: `SuperAdmin`, `BranchManager`, `Manager`, `Emp`. Seeded admin user is in `DbSeeder.cs`.
- Permissions: ManageUsers, ManageBranches, ManageDepartments, ManageOffices, ManageRoles, ViewVacations, CreateVacation, ApproveVacation, ManageVacations, ManageVacationTypes.

## Vacation business rules (core domain) — reviewed & hardened 2026-09-27
- Status flow: `PendingManager (1)` → `PendingBranchManager (3)` → `Approved (4)`; any stage can → `Rejected (5)` (stores RejectedByUserId, reason, date).
  `PendingAdministrative` was removed entirely 2026-09-27 (enum member, translations, `GetPendingForAdministrativeAsync`, and the status conditions in `VacationService`'s day-count/overlap queries). Its old int value (`2`) is left unassigned/reserved in the enum comment — never reuse it.
  A `Cancelled (6)` status was added the same day: the vacation's owner can cancel it (soft — `Status = Cancelled`, record kept for audit) only while it's `PendingManager` or `PendingBranchManager`; blocked once `Approved`/`Rejected`/already `Cancelled`. Endpoint: `PUT api/Vacations/Cancel/{id}`, policy `CreateVacation`. `Cancelled` is intentionally excluded from `HasOverlappingVacationAsync` and the paid/unpaid day-count queries — cancelling frees up that period and doesn't count against the monthly quota.
- Approver checks (`ApproveVacationCommandHandler`): role `Manager` in same department for stage 1; role `BranchManager` in same branch for stage 2; `SuperAdmin` bypasses both. Role checks are by role **name**. Self-approval is blocked (`vacation.UserId == currentUser.Id` → Unauthorized) for everyone except SuperAdmin.
- Create (`CreateVacationCommandHandler`): end ≥ start, no overlap with pending/approved vacations, `VacDayCount = days inclusive`, Department/Branch copied from the user. **Returns `List<VacationResponseDto>`, not a single one** — see paid-day splitting below.
- Paid rule (day-level splitting, confirmed with user 2026-09-27): if `VacationType.IsPaid == false` → whole request unpaid. Otherwise max **2 paid days per month, pooled across all IsPaid=true types** (regular paid + sick + overtime-reward share the same monthly cap). If a single request would cross the cap mid-range, `BuildPaidUnpaidSegmentsAsync` walks day-by-day and **splits it into separate `Vacation` rows** — one covering the still-available paid days, another (or more, if it spans several months) covering the rest as unpaid. Each resulting row goes through the normal independent approval workflow.
- Visibility/scoping (this was broken before the review — controller had zero policies applied despite policies existing, and `Emp` role had **no vacation permissions at all** in `DbSeeder`, silently masked by that same gap):
  - `Emp` role now seeded with `ViewVacations` + `CreateVacation`. **Anyone with an old JWT needs to re-login** to pick up new permissions (they're baked into the token at login).
  - `GetAll` is now role-aware ("vacations relevant to me"): SuperAdmin → everything, BranchManager → own branch (`GetByBranchIdAsync`), Manager → own department (`GetByDepartmentIdAsync`), anyone else → own only. This is how "رئيس القسم يرى إجازات قسمه" / "رئيس الفرع يرى إجازات فرعه" is satisfied for *all* statuses, not just pending ones (`PendingForMe` only ever showed pending).
  - `GetById`/`User/{userId}` now check ownership/scope inside the handler (owner, or the dept Manager, or the branch BranchManager, or SuperAdmin) — previously any authenticated user could view anyone's vacation by id/userId.
- Endpoints (`api/Vacations`, all now behind proper policies): `Create` (form, policy `CreateVacation`), `Cancel/{id}` (PUT, policy `CreateVacation`, owner-only), `Approve/{id}` (PUT body `{approve, reason}`, policy `ApproveVacation`), `PendingForMe` (policy `ApproveVacation`), `Get/{id}` / `GetAll` / `User/{userId}` / `My` (policy `ViewVacations`, scoped per role as above).

## Running
- API: `dotnet run --project API` → https://localhost:7181 (Swagger at `/swagger`), http://localhost:5204.
- Client: `cd EWMS.Client && npm start` → http://localhost:4200; `proxy.conf.json` proxies `/api` to https://localhost:7181. CORS policy `AllowAngular` in `Program.cs`.

## Offices (added 2026-09-27)
- CRUD at `api/Offices` (`Create`/`Update`/`Delete`/`Get/{id}`/`GetAll`), policy `ManageOffices`. Scoping mirrors Departments: non-SuperAdmin can only manage offices in their own `DepartmentId`.
- `Users/Create` and `Users/Update` now require `OfficeId` (validated to belong to the given `DepartmentId`, same pattern as Department↔Branch validation in `UserRules.EnsureUserReferencesAsync`).
- Deleting a Department/Office that still has children throws a friendly `InvalidOperationException` (`HasOfficesAsync` / `HasUsersAsync` checks) rather than letting the DB FK-restrict raise a raw 500 — **always add this guard for any new parent→child relationship**, it's easy to forget.
- Migration `Add_User_OfficeId` was hand-edited (not left as EF auto-generated) because the DB already had existing Users: it adds the column nullable, backfills a "مكتب افتراضي - <Department>" placeholder office + assigns existing users to it, then tightens to NOT NULL. Any future required-FK-on-populated-table migration should follow this same backfill pattern.

## Notifications (added 2026-09-27)
- New `Domain.Entities.Notification` (recipient `UserId`, `Title`, `Message`, `Type` enum, optional `RelatedEntityType`/`RelatedEntityId` for deep-linking, `IsRead`/`ReadAt`). Cascade-deletes with its recipient `User`.
- **In-app/REST inbox, not real-time.** No SignalR/websocket in this project — the client polls `api/Notifications/My` / `UnreadCount`. If real-time push is ever wanted, that's a separate, much larger addition (hub + JWT-over-websocket auth) — don't assume it's already wired.
- `Application/Features/Vacations/VacationNotifier.cs` is a static dispatch helper (same pattern as `UserRules`) called from `CreateVacationCommandHandler`, `ApproveVacationCommandHandler`, and `CancelVacationCommandHandler`. It resolves recipients by role+department/branch via the existing `IUserService.GetByDepartmentAsync`/`GetByBranchAsync` (filtered client-side to `Role.Name == "Manager"`/`"BranchManager"`) — no new user-lookup repo methods were needed.
- Full trigger map (verified live end-to-end): submit → dept Manager(s); Manager approves → employee + branch Manager(s) (forwarded); Manager rejects → employee only (branch never sees it); BranchManager approves (final) → employee + dept Manager(s); BranchManager rejects (final) → employee + dept Manager(s); employee cancels → dept Manager(s) always, **plus** branch Manager(s) too if it had already reached `PendingBranchManager`.
- A `CreateVacationCommand` that splits into paid+unpaid segments (see above) fires one `VacationSubmitted` notification per segment/entity created.
- Endpoints (`api/Notifications`, plain `[Authorize]`, every action implicitly scoped to the caller): `GET My?unreadOnly=`, `GET UnreadCount`, `PUT MarkAsRead/{id}` (403/401 if not the owner), `PUT MarkAllAsRead`.
- If you add a new workflow with async multi-party approval later, follow this same shape: a `Notification` row per recipient, a static `<Feature>Notifier` helper, called from inside the command handler right after `UpdateAsync`/`AddAsync` — don't invent a different mechanism.

## Known issues / tech debt (not fixed yet)
1. Angular client is out of sync: `core/services/ewms.service.ts` and `features/projects` still call deleted `/Projects/...` endpoints; no Vacations / VacationTypes / Offices / Notifications pages exist. The Users form/model also needs an `OfficeId` field now, and any Vacation-create UI must handle the endpoint now returning a **list**.
2. `Vacation.BranchManagerAccept` defaults to `false` in the entity but `HasDefaultValue(true)` in `DataContext`.
3. Project leftovers: `Stage` (has `ProjectId`), `Tasks`, `EmpReport`, empty `StageTasks`/`State`, `API/wwwroot/uploads/projects/`, and "Project" policies in `AddAPIRigstrationServices.cs`.
4. JWT signing key is hard-coded in `API/appsettings.json` — should move to user-secrets/env before deployment.
5. Some namespaces are inconsistent (`...Vacations.Query.GetAll` vs `...Queries.GetByUser`; folder `Department/Command` vs `Branches/Commands`); match the file you're editing.

## Conventions
- Git branch in use: `Khaled`; main branch `master`.
- Keep controllers thin; business logic lives in Handlers; data access in Infrastructure services.
- Error messages to users in Arabic.
