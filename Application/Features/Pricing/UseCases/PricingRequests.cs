using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Pricing.PricingSupport;

namespace Application.Features.Pricing
{
    // طلبات التسعير: البحث ضمن حدّ الفرع، التفصيل (بالمقارنة بنداً بنداً)، التسجيل، التعديل، التكليف، والإلغاء. الطلب لا يُحذف: يُلغى.

    /// <param name="Mine">true = ما سجّلته أو كُلِّفت به</param>
    public record SearchPricingRequestsQuery(int? BranchId, int? ProjectId, int? Status, string? Search, bool OverdueOnly, bool Mine, int Page, int PageSize)
        : IRequest<PagedResultDto<PricingListItemDto>>;
    public record GetPricingRequestQuery(int Id) : IRequest<PricingDetailDto>;
    public record GetPricingAssigneesQuery(int? BranchId) : IRequest<List<PricingAssigneeDto>>;
    public record CreatePricingRequestCommand(PricingRequestRequestDto Dto) : IRequest<PricingDetailDto>;
    public record UpdatePricingRequestCommand(int Id, PricingRequestRequestDto Dto) : IRequest<PricingDetailDto>;
    public record AssignPricingRequestCommand(int Id, AssignPricingRequestDto Dto) : IRequest<PricingDetailDto>;
    public record CancelPricingRequestCommand(int Id, PricingStepRequestDto Dto) : IRequest<PricingDetailDto>;

    internal static class PricingInput
    {
        public static List<PricingRules.ItemInput> Inputs(PricingRequestRequestDto dto) =>
            dto.Items.Select(i => new PricingRules.ItemInput(i.Description, i.Specifications, i.Quantity, i.Unit)).ToList();

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public static bool SameItems(IEnumerable<PricingRequestItem> existing, IReadOnlyList<PricingItemRequestDto> sent)
        {
            var current = existing.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
            if (current.Count != sent.Count) return false;
            return current.Zip(sent).All(p => p.First.Description == p.Second.Description.Trim() && p.First.Quantity == p.Second.Quantity
                && p.First.Unit == p.Second.Unit.Trim() && p.First.Specifications == (p.Second.Specifications ?? string.Empty).Trim());
        }

        public static List<PricingRequestItem> NewItems(PricingRequestRequestDto dto) =>
            dto.Items.Select((i, index) => new PricingRequestItem
            {
                SortOrder = index + 1, Description = i.Description.Trim(), Specifications = (i.Specifications ?? string.Empty).Trim(), Quantity = i.Quantity, Unit = i.Unit.Trim()
            }).ToList();
    }

    public class SearchPricingRequestsHandler(PricingContext ctx) : IRequestHandler<SearchPricingRequestsQuery, PagedResultDto<PricingListItemDto>>
    {
        public async Task<PagedResultDto<PricingListItemDto>> Handle(SearchPricingRequestsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!AppPermissions.AnyOf["AnyPricingRequestView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض طلبات التسعير");
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var today = DateTime.Today;

            PricingRequestStatus? status = request.Status is int s && Enum.IsDefined(typeof(PricingRequestStatus), s) ? (PricingRequestStatus)s : null;
            var filter = new PricingFilter(viewer.IsOrganizationWide ? request.BranchId : null, request.ProjectId, status, request.Search, request.OverdueOnly, request.Mine ? viewer.Id : null, today);

            var (items, total) = await ctx.Pricing.SearchAsync(PricingRules.Visible(viewer), filter, page, pageSize);
            return new PagedResultDto<PricingListItemDto>
            {
                Items = items.Select(r => ToListItem(r, today)).ToList(), TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }

    public class GetPricingRequestHandler(PricingContext ctx) : IRequestHandler<GetPricingRequestQuery, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(GetPricingRequestQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var item = await ctx.LoadAsync(viewer, request.Id);
            return ToDetail(item, viewer, DateTime.Today);
        }
    }

    public class CreatePricingRequestHandler(PricingContext ctx) : IRequestHandler<CreatePricingRequestCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(CreatePricingRequestCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var project = dto.ProjectId is int pid ? await ctx.Projects.GetByIdAsync(pid) ?? throw new KeyNotFoundException("المشروع غير موجود") : null;
            var contract = dto.ContractId is int cid ? await ctx.Contracts.GetByIdAsync(cid) ?? throw new KeyNotFoundException("العقد غير موجود") : null;
            PricingRules.EnsureCanRegister(viewer, project, contract);
            if (PricingRules.ValidateRequest(dto.Title, dto.Description, dto.DueDate, PricingInput.Inputs(dto), DateTime.Today) is string problem)
                throw new InvalidOperationException(problem);
            var branch = PricingContext.ResolveBranch(viewer, project, dto.BranchId);
            var correspondence = await ctx.ResolveCorrespondenceAsync(viewer, dto.CorrespondenceId, branch);

            var created = await ctx.Pricing.AddAsync(new PricingRequest
            {
                BranchId = branch, ProjectId = project?.Id, ContractId = contract?.Id, CorrespondenceId = correspondence?.Id, Title = dto.Title.Trim(),
                Description = (dto.Description ?? string.Empty).Trim(), OutsideContractScope = dto.OutsideContractScope, DueDate = dto.DueDate?.Date,
                Status = PricingRequestStatus.Registered, RegisteredByUserId = viewer.Id, Items = PricingInput.NewItems(dto)
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.PricingRequest, null, $"تسجيل طلب تسعير «{dto.Title.Trim()}»",
                Lines($"البنود: {dto.Items.Count}", dto.OutsideContractScope ? "خارج نطاق العقد" : null, project is null ? null : $"المشروع: {project.Name}")));

            await ctx.NotifyRequestAsync(await ctx.HoldersAsync(branch, AppPermissions.AssignPricingRequest), viewer.Id, created, NotificationType.PricingRequestAssigned,
                "طلب تسعير جديد بانتظار التكليف", $"سجّل {viewer.User.FullName} الطلب {PricingContext.Label(created)}");
            return await ctx.DetailAsync(viewer, created.Id);
        }
    }

    public class UpdatePricingRequestHandler(PricingContext ctx) : IRequestHandler<UpdatePricingRequestCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(UpdatePricingRequestCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var item = await ctx.LoadAsync(viewer, request.Id);
            PricingRules.EnsureCanEdit(viewer, item);
            if (dto.ProjectId != item.ProjectId) throw new InvalidOperationException("لا يُنقل الطلب إلى مشروع آخر");
            if (PricingRules.ValidateRequest(dto.Title, dto.Description, dto.DueDate, PricingInput.Inputs(dto), DateTime.Today) is string problem)
                throw new InvalidOperationException(problem);

            var contract = dto.ContractId is int cid ? await ctx.Contracts.GetByIdAsync(cid) ?? throw new KeyNotFoundException("العقد غير موجود") : null;
            if (contract != null && contract.ProjectId != item.ProjectId) throw new InvalidOperationException("العقد من مشروع آخر");
            var correspondence = await ctx.ResolveCorrespondenceAsync(viewer, dto.CorrespondenceId, item.BranchId);

            var sameItems = PricingInput.SameItems(item.Items, dto.Items);
            if (!sameItems) PricingRules.EnsureItemsEditable(item);

            var details = Lines(
                Change("العنوان", item.Title, dto.Title), Change("الوصف", item.Description, dto.Description),
                Change("خارج نطاق العقد", item.OutsideContractScope ? "نعم" : "لا", dto.OutsideContractScope ? "نعم" : "لا"),
                Change("الموعد", item.DueDate?.ToString("yyyy-MM-dd"), dto.DueDate?.ToString("yyyy-MM-dd")),
                sameItems ? null : "تغيّرت البنود");

            item.Title = dto.Title.Trim();
            item.Description = (dto.Description ?? string.Empty).Trim();
            item.OutsideContractScope = dto.OutsideContractScope;
            item.DueDate = dto.DueDate?.Date;
            item.ContractId = contract?.Id;
            item.CorrespondenceId = correspondence?.Id;

            var audit = new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.PricingRequest, item.Id, $"تعديل طلب التسعير {PricingContext.Label(item)}", details);
            if (sameItems) await ctx.Pricing.UpdateAsync(item, dto.RowVersion, audit);
            else await ctx.Pricing.ReplaceItemsAsync(item, item.Items.ToList(), PricingInput.NewItems(dto), dto.RowVersion, audit);
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    /// <summary>التكليف: موظف المكتب (نشط من الفرع ويملك صلاحية إعداد العروض) يتابع الطلب؛ ويتحول الطلب المسجَّل إلى «قيد جمع العروض»</summary>
    /// <summary>من يصلح للتكليف: حاملو إعداد العروض في فرع المستخدم (أو الفرع المطلوب لمن يرى كل الفروع)</summary>
    public class GetPricingAssigneesHandler(PricingContext ctx) : IRequestHandler<GetPricingAssigneesQuery, List<PricingAssigneeDto>>
    {
        public async Task<List<PricingAssigneeDto>> Handle(GetPricingAssigneesQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (branch == null) return [];
            var holders = await ctx.Permissions.GetUsersWithPermissionAsync(AppPermissions.PreparePricingOffers, branch);
            return holders.GroupBy(u => u.Id).Select(g => g.First()).OrderBy(u => u.FullName)
                .Select(u => new PricingAssigneeDto { Id = u.Id, FullName = u.FullName }).ToList();
        }
    }

    public class AssignPricingRequestHandler(PricingContext ctx) : IRequestHandler<AssignPricingRequestCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(AssignPricingRequestCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var item = await ctx.LoadAsync(viewer, request.Id);
            var assignee = await ctx.Users.GetByIdAsync(dto.AssigneeUserId) ?? throw new KeyNotFoundException("الموظف غير موجود");
            var granted = await ctx.Permissions.GetAsync(assignee.Id);
            var canPrepare = OrganizationRole.IsSystemAdmin(assignee) || granted.Contains(AppPermissions.PreparePricingOffers);
            PricingRules.EnsureCanAssign(viewer, item, assignee, canPrepare);

            var previous = item.Assignee?.FullName;
            item.AssigneeUserId = assignee.Id;
            item.AssignedAt = DateTime.UtcNow;
            item.AssignedByUserId = viewer.Id;
            if (item.Status == PricingRequestStatus.Registered) item.Status = PricingRequestStatus.Collecting;

            await ctx.Pricing.UpdateAsync(item, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.PricingRequest, item.Id,
                    $"{(previous is null ? "تكليف" : "تغيير المكلَّف في")} طلب التسعير {PricingContext.Label(item)}", $"المكلَّف: {previous ?? "—"} ← {assignee.FullName}"));
            await ctx.NotifyRequestAsync([assignee.Id], viewer.Id, item, NotificationType.PricingRequestAssigned, "كُلِّفت بطلب تسعير",
                $"كلّفك {viewer.User.FullName} بمتابعة الطلب {PricingContext.Label(item)}: سجّل العروض وقارنها");
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }

    public class CancelPricingRequestHandler(PricingContext ctx) : IRequestHandler<CancelPricingRequestCommand, PricingDetailDto>
    {
        public async Task<PricingDetailDto> Handle(CancelPricingRequestCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var item = await ctx.LoadAsync(viewer, request.Id);
            PricingRules.EnsureCanCancel(viewer, item);

            item.Status = PricingRequestStatus.Cancelled;
            await ctx.Pricing.UpdateAsync(item, request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Cancelled, AuditEntities.PricingRequest, item.Id, $"إلغاء طلب التسعير {PricingContext.Label(item)}",
                    string.IsNullOrWhiteSpace(request.Dto.Note) ? string.Empty : $"السبب: {request.Dto.Note.Trim()}"));
            return await ctx.DetailAsync(viewer, item.Id);
        }
    }
}
