# EWMS — Employee Workflow Management System

Internal HR system: branches, departments, offices, users, roles/permissions, and a multi-stage **vacation request workflow**.

Org hierarchy: **Branch → Department → Office → User**. `Vacation` approval scoping deliberately stays at Department/Branch level only — it does NOT use Office (confirmed with the user 2026-09-27).
The old "Projects" feature was removed in commit `ba670e2` and replaced by Vacations.
Code comments and user-facing messages are in Arabic; keep that convention.

## Stack
- Backend: ASP.NET Core **.NET 10**, EF Core 10 + SQL Server (LocalDB, DB name `EWMS`), MediatR 12, FluentValidation 12, AutoMapper 16, JWT Bearer, BCrypt.
- Frontend: **Angular 22** standalone components. **Active UI is `F:\-EWMS-UI-main-main`** (separate folder, not a git repo; chosen by the user 2026-09-27). `EWMS.Client/` from older notes does not exist here.
- Solution file: `EWMS.slnx` (API, Application, Domain, Infrastructure). `EWMS/Class1.cs` is an empty leftover, not in the solution.

## Architecture (Clean Architecture + CQRS)
Dependencies: `API → Application, Infrastructure`; `Infrastructure → Application, Domain`; `Application → Domain`.

- **Domain/** — entities (`User`, `Branch`, `Department`, `Office`, `Role`, `Permission`, `RolePermission`, `Vacation`, `VacationType`, `UserToken`) and `Enums/VacationStatus`. `User.BranchId/DepartmentId/OfficeId` are **nullable and role-dependent** (see "User placement by role" below); `Office.DepartmentId` is required (belongs to exactly one department).
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
- Permissions: ManageUsers, ManageBranches, ManageDepartments, ManageOffices, ManageRoles, ViewVacations, CreateVacation, ApproveVacation, ManageVacations, ManageVacationTypes, ViewDevices, ManageDevices.

## Vacation business rules (core domain) — reviewed & hardened 2026-09-27
- Status flow: `PendingManager (1)` → `PendingBranchManager (3)` → `Approved (4)`; any stage can → `Rejected (5)` (stores RejectedByUserId, reason, date).
  `PendingAdministrative` was removed entirely 2026-09-27 (enum member, translations, `GetPendingForAdministrativeAsync`, and the status conditions in `VacationService`'s day-count/overlap queries). Its old int value (`2`) is left unassigned/reserved in the enum comment — never reuse it.
  A `Cancelled (6)` status was added the same day: the vacation's owner can cancel it (soft — `Status = Cancelled`, record kept for audit) only while it's `PendingManager` or `PendingBranchManager`; blocked once `Approved`/`Rejected`/already `Cancelled`. Endpoint: `PUT api/Vacations/Cancel/{id}`, policy `CreateVacation`. `Cancelled` is intentionally excluded from `HasOverlappingVacationAsync` and the paid/unpaid day-count queries — cancelling frees up that period and doesn't count against the monthly quota.
- Approver checks (`ApproveVacationCommandHandler`): role `Manager` in same department for stage 1; role `BranchManager` in same branch for stage 2; `SuperAdmin` bypasses both. Role checks are by role **name**. Self-approval is blocked (`vacation.UserId == currentUser.Id` → Unauthorized) for everyone except SuperAdmin.
- Create (`CreateVacationCommandHandler`): **BranchManager and SuperAdmin cannot submit vacations** (no department → `InvalidOperationException`; UI hides the form). A **Manager's own vacation skips stage 1** and starts at `PendingBranchManager` (so it's never stuck needing another Manager). End ≥ start, no overlap with pending/approved vacations, `VacDayCount = days inclusive`, Department/Branch copied from the user. **Returns `List<VacationResponseDto>`, not a single one** — see paid-day splitting below.
- Paid rule (day-level splitting, confirmed with user 2026-09-27): if `VacationType.IsPaid == false` → whole request unpaid. Otherwise max **2 paid days per month, pooled across all IsPaid=true types** (regular paid + sick + overtime-reward share the same monthly cap). If a single request would cross the cap mid-range, `BuildPaidUnpaidSegmentsAsync` walks day-by-day and **splits it into separate `Vacation` rows** — one covering the still-available paid days, another (or more, if it spans several months) covering the rest as unpaid. Each resulting row goes through the normal independent approval workflow.
- Visibility/scoping (this was broken before the review — controller had zero policies applied despite policies existing, and `Emp` role had **no vacation permissions at all** in `DbSeeder`, silently masked by that same gap):
  - `Emp` role now seeded with `ViewVacations` + `CreateVacation`. **Anyone with an old JWT needs to re-login** to pick up new permissions (they're baked into the token at login).
  - `GetAll` is now role-aware ("vacations relevant to me"): SuperAdmin → everything, BranchManager → own branch (`GetByBranchIdAsync`), Manager → own department (`GetByDepartmentIdAsync`), anyone else → own only. This is how "رئيس القسم يرى إجازات قسمه" / "رئيس الفرع يرى إجازات فرعه" is satisfied for *all* statuses, not just pending ones (`PendingForMe` only ever showed pending).
  - `GetById`/`User/{userId}` now check ownership/scope inside the handler (owner, or the dept Manager, or the branch BranchManager, or SuperAdmin) — previously any authenticated user could view anyone's vacation by id/userId.
- Endpoints (`api/Vacations`, all now behind proper policies): `Create` (form, policy `CreateVacation`), `Cancel/{id}` (PUT, policy `CreateVacation`, owner-only), `Approve/{id}` (PUT body `{approve, reason}`, policy `ApproveVacation`), `PendingForMe` (policy `ApproveVacation`), `Get/{id}` / `GetAll` / `User/{userId}` / `My` (policy `ViewVacations`, scoped per role as above).

## Running
- API: `dotnet run --project API` → https://localhost:7181 (Swagger at `/swagger`), http://localhost:5204.
- Client: `cd F:\-EWMS-UI-main-main && npm start` (restart it after any `proxy.conf.json` change) → http://localhost:4200; `proxy.conf.json` proxies `/api` to https://localhost:7181. CORS policy `AllowAngular` in `Program.cs`.

## Offices (added 2026-09-27)
- CRUD at `api/Offices` (`Create`/`Update`/`Delete`/`Get/{id}`/`GetAll`), policy `ManageOffices`. Scoping (`Offices/OfficeRules`): a user with a department (Manager) manages only their department's offices; a user without one (BranchManager) manages offices of every department in their branch; SuperAdmin everything.
- **User placement by role** (decided with the user 2026-09-27, `Application/Features/Users/UserPlacement.cs`, mirrored in UI `core/utils/user-placement.ts` — change both together):
  SuperAdmin → no branch/department/office; BranchManager → branch only; Manager → branch + department (no office); Emp and any custom role → branch + department + office.
  `UserRules.EnsureUserReferencesAsync` validates only what the role needs (office ∈ department ∈ branch) and returns the normalized placement — fields the role doesn't need are dropped to NULL even if sent. Migration `Make_User_Placement_Optional` made the columns nullable and NULLed existing data to match. JWT `DepartmentId`/`BranchId` claims are empty (→ 0 in `ICurrentUserService`) when absent; users must re-login after their placement changes.
  Legacy `POST api/Auth/register` (`AuthService.RegisterAsync`, unused by the UI) still requires all three — not migrated to this rule.
- Vacation notifications exclude the requester from manager recipient lists, and dept Managers only get final-decision/cancel notices if the vacation actually passed through them (`ManagerAccept`).
- Deleting a Department/Office that still has children throws a friendly `InvalidOperationException` (`HasOfficesAsync` / `HasUsersAsync` checks) rather than letting the DB FK-restrict raise a raw 500 — **always add this guard for any new parent→child relationship**, it's easy to forget.
- Migration `Add_User_OfficeId` was hand-edited (not left as EF auto-generated) because the DB already had existing Users: it adds the column nullable, backfills a "مكتب افتراضي - <Department>" placeholder office + assigns existing users to it, then tightens to NOT NULL. Any future required-FK-on-populated-table migration should follow this same backfill pattern.

## Notifications (added 2026-09-27)
- New `Domain.Entities.Notification` (recipient `UserId`, `Title`, `Message`, `Type` enum, optional `RelatedEntityType`/`RelatedEntityId` for deep-linking, `IsRead`/`ReadAt`). Cascade-deletes with its recipient `User`.
- **Real-time push via SignalR (added 2026-09-27).** Hub `API/Hubs/NotificationHub` at `/hubs/notifications` (`[Authorize]`, server→client only, method name `notification`, payload `NotificationResponseDto`). Routing is `Clients.User(UserId)` via the default NameIdentifier user-id provider.
  - Push is automatic: Infrastructure `NotificationService.AddAsync/AddRangeAsync` call `INotificationPusher` (Application interface, implemented by `API/Hubs/SignalRNotificationPusher`, registered in `AddAPIRigstrationServices`) **after** SaveChanges. Push failures are logged and swallowed — the DB row is the source of truth. So any new notifier gets real-time for free; don't call the hub directly.
  - JWT over websocket: `OnMessageReceived` accepts `?access_token=` **only** for `/hubs/*`, and `OnTokenValidated` falls back to that query token so the revoked-token check still applies. An already-open socket is not killed on logout; the client stops it itself.
  - UI: `NotificationService.start()` (SignalR + auto-reconnect, 60s fallback poll of `UnreadCount`), toast popups (`features/layout/notification-toasts.ts`), browser desktop `Notification` when the tab is hidden (opt-in button on the notifications page). `proxy.conf.json` has a `/hubs` entry with `ws: true` — changing it requires restarting `ng serve`.
- `Application/Features/Vacations/VacationNotifier.cs` is a static dispatch helper (same pattern as `UserRules`) called from `CreateVacationCommandHandler`, `ApproveVacationCommandHandler`, and `CancelVacationCommandHandler`. It resolves recipients by role+department/branch via the existing `IUserService.GetByDepartmentAsync`/`GetByBranchAsync` (filtered client-side to `Role.Name == "Manager"`/`"BranchManager"`) — no new user-lookup repo methods were needed.
- Full trigger map (verified live end-to-end): submit → dept Manager(s); Manager approves → employee + branch Manager(s) (forwarded); Manager rejects → employee only (branch never sees it); BranchManager approves (final) → employee + dept Manager(s); BranchManager rejects (final) → employee + dept Manager(s); employee cancels → dept Manager(s) always, **plus** branch Manager(s) too if it had already reached `PendingBranchManager`.
- A `CreateVacationCommand` that splits into paid+unpaid segments (see above) fires one `VacationSubmitted` notification per segment/entity created.
- Endpoints (`api/Notifications`, plain `[Authorize]`, every action implicitly scoped to the caller): `GET My?unreadOnly=`, `GET UnreadCount`, `PUT MarkAsRead/{id}` (403/401 if not the owner), `PUT MarkAllAsRead`.
- If you add a new workflow with async multi-party approval later, follow this same shape: a `Notification` row per recipient, a static `<Feature>Notifier` helper, called from inside the command handler right after `UpdateAsync`/`AddAsync` — don't invent a different mechanism.

## Frontend ↔ backend wiring (2026-09-27)
- `GET api/Auth/Me` (plain `[Authorize]`, `Users/Queries/GetMe`) returns the caller's `UserResponseDto` — used by the UI profile page.
- `ExceptionMiddleware`: `UnauthorizedAccessException` → **403 when the caller is authenticated**, 401 otherwise. The UI's JWT interceptor logs out on 401, so a business "not allowed" must never come back as 401.
- `Branches/GetAll` still requires `ManageBranches` (loosening it was declined). The UI's `EwmsService.getBranchLookup()` derives the branch dropdown from `Department/GetAll` for users without that permission — keep that in mind before changing either endpoint.
- UI: review page/nav gated on the `ApproveVacation` permission (not role names); notifications page + unread badge polling `UnreadCount` every 30s; profile page handles the split-create list response and cancel; Projects UI removed.

## Device inventory: Region → Site → Device (added 2026-09-28)
- Pure documentation/inventory feature for the technical branch's operations team (replaces scattered Excel sheets) — **deliberately independent of the Branch/Department/Office org hierarchy**: no FK to any of them, no per-branch/department scoping in handlers. Access is controlled purely by two flat permissions, `ViewDevices` (read) and `ManageDevices` (write), covering all four entities below. Only `SuperAdmin` gets them by default in `DbSeeder`; grant them to whichever role represents the technical/operations team via the existing `ManageRoles` UI.
- Entities (`Domain/Entities`): `Region` (Name*, Description) 1→N `Site` (Name*, Description, Location — free-text, e.g. Google Maps coordinates, may be empty, RegionId*). `Device` (Name*, Model, SN, Description) is standalone; `DeviceSite` is the many-to-many join table between `Device` and `Site` (a device can be installed at multiple sites, a site can host multiple devices) and carries the per-installation connection info: `Ip*`, `SubnetMask*` (both validated as IPv4 in the command validators), `UserName*`, `Pass*`, `Note`. Unique index on `(DeviceId, SiteId)` — a device can only be linked once to the same site.
- **`Pass` is stored as plain text** (decided with the user 2026-09-28) — these are live device credentials needed for actual reversible use, not hashed like user passwords. Flagged here as the same category of tech debt as the hard-coded JWT key (#4 below); revisit if this ever needs to be more defensible (e.g. ASP.NET Data Protection API).
- `Region.Name` and `Site.Name` are globally unique (like `Office.Name`); `Device.Name`/`SN` are not — multiple devices can plausibly share a name/model, so no uniqueness is enforced there.
- Delete guards (matching the Office/Department pattern): can't delete a `Region` with `Site`s (`HasSitesAsync`), can't delete a `Site` with `DeviceSite` links (`HasDeviceLinksAsync`), can't delete a `Device` with `DeviceSite` links (`HasSiteLinksAsync`) — all throw a friendly `InvalidOperationException` instead of a raw FK-restrict 500.
- Endpoints, all under the two policies above (no per-entity permission split): `api/Regions`, `api/Sites` (`GetAll` takes optional `?regionId=`), `api/Devices`, `api/DeviceSites` (`GetAll` takes optional `?siteId=`/`?deviceId=`) — each with `Create`/`Update/{id}`/`Delete/{id}`/`Get/{id}`/`GetAll`, `[FromForm]` bodies, matching the Offices controller shape exactly.
- Migration `Add_DeviceInventory` creates all four tables in one migration (no existing data to backfill, unlike `Add_User_OfficeId`).

## Known issues / tech debt (not fixed yet)
1. (Angular client sync — resolved, see "Frontend ↔ backend wiring" above.)
2. `Vacation.BranchManagerAccept` defaults to `false` in the entity but `HasDefaultValue(true)` in `DataContext`.
3. Project leftovers: `Stage` (has `ProjectId`), `Tasks`, `EmpReport`, empty `StageTasks`/`State`, `API/wwwroot/uploads/projects/`, and "Project" policies in `AddAPIRigstrationServices.cs`.
4. JWT signing key is hard-coded in `API/appsettings.json` — should move to user-secrets/env before deployment. Same category: `DeviceSite.Pass` is stored as plain text (see "Device inventory" above).
5. Some namespaces are inconsistent (`...Vacations.Query.GetAll` vs `...Queries.GetByUser`; folder `Department/Command` vs `Branches/Commands`); match the file you're editing.

## Conventions
- Git branch in use: `Khaled`; main branch `master`.
- Keep controllers thin; business logic lives in Handlers; data access in Infrastructure services.
- Error messages to users in Arabic.
