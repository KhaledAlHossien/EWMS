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

## Vacation business rules (core domain)
- Status flow: `PendingManager (1)` → `PendingBranchManager (3)` → `Approved (4)`; any stage can → `Rejected (5)` (stores RejectedByUserId, reason, date).
  `PendingAdministrative (2)` is **no longer used** in the flow (administrative stage removed in latest commit) but still appears in some queries.
- Approver checks (`ApproveVacationCommandHandler`): role `Manager` in same department for stage 1; role `BranchManager` in same branch for stage 2; `SuperAdmin` bypasses both. Role checks are by role **name**.
- Create (`CreateVacationCommandHandler`): end ≥ start, no overlap with pending/approved vacations, `VacDayCount = days inclusive`, Department/Branch copied from the user.
- Paid rule: if `VacationType.IsPaid == false` → unpaid. Otherwise max **2 paid days per month**; if the new request would exceed it in any month, the whole request becomes unpaid.
- Endpoints (`api/Vacations`): `Create` (form), `Approve/{id}` (PUT body `{approve, reason}`), `PendingForMe`, `Get/{id}`, `GetAll`, `User/{userId}`, `My`.

## Running
- API: `dotnet run --project API` → https://localhost:7181 (Swagger at `/swagger`), http://localhost:5204.
- Client: `cd EWMS.Client && npm start` → http://localhost:4200; `proxy.conf.json` proxies `/api` to https://localhost:7181. CORS policy `AllowAngular` in `Program.cs`.

## Offices (added 2026-09-27)
- CRUD at `api/Offices` (`Create`/`Update`/`Delete`/`Get/{id}`/`GetAll`), policy `ManageOffices`. Scoping mirrors Departments: non-SuperAdmin can only manage offices in their own `DepartmentId`.
- `Users/Create` and `Users/Update` now require `OfficeId` (validated to belong to the given `DepartmentId`, same pattern as Department↔Branch validation in `UserRules.EnsureUserReferencesAsync`).
- Deleting a Department/Office that still has children throws a friendly `InvalidOperationException` (`HasOfficesAsync` / `HasUsersAsync` checks) rather than letting the DB FK-restrict raise a raw 500 — **always add this guard for any new parent→child relationship**, it's easy to forget.
- Migration `Add_User_OfficeId` was hand-edited (not left as EF auto-generated) because the DB already had existing Users: it adds the column nullable, backfills a "مكتب افتراضي - <Department>" placeholder office + assigns existing users to it, then tightens to NOT NULL. Any future required-FK-on-populated-table migration should follow this same backfill pattern.

## Known issues / tech debt (not fixed yet)
1. Angular client is out of sync: `core/services/ewms.service.ts` and `features/projects` still call deleted `/Projects/...` endpoints; no Vacations / VacationTypes / Offices pages exist. The Users form/model also needs an `OfficeId` field now.
2. `VacationsController` has only `[Authorize]` — no permission policies applied (any logged-in user can hit `GetAll`, `User/{id}`).
3. `Vacation.BranchManagerAccept` defaults to `false` in the entity but `HasDefaultValue(true)` in `DataContext`.
4. Project leftovers: `Stage` (has `ProjectId`), `Tasks`, `EmpReport`, empty `StageTasks`/`State`, `API/wwwroot/uploads/projects/`, and "Project" policies in `AddAPIRigstrationServices.cs`.
5. JWT signing key is hard-coded in `API/appsettings.json` — should move to user-secrets/env before deployment.
6. Some namespaces are inconsistent (`...Vacations.Query.GetAll` vs `...Queries.GetByUser`; folder `Department/Command` vs `Branches/Commands`); match the file you're editing.

## Conventions
- Git branch in use: `Khaled`; main branch `master`.
- Keep controllers thin; business logic lives in Handlers; data access in Infrastructure services.
- Error messages to users in Arabic.
