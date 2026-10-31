using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Handover.HandoverSupport;

namespace Application.Features.Handover
{
    // محاضر الاستلام: البحث ضمن حدّ المشاريع، التفصيل، تقديم الطلب (أولي أو نهائي)، إلغاؤه، وتنبيه إغلاق المشروع. المحضر لا يُحذف: يُلغى.

    public record SearchHandoversQuery(int? ProjectId, int? BranchId, int? Kind, int? Status, string? Search, bool OpenNotesOnly, int Page, int PageSize)
        : IRequest<PagedResultDto<HandoverListItemDto>>;
    public record GetHandoverQuery(int Id) : IRequest<HandoverDetailDto>;
    public record RequestHandoverCommand(HandoverRequestDto Dto) : IRequest<HandoverDetailDto>;
    public record CancelHandoverCommand(int Id, CancelHandoverRequestDto Dto) : IRequest<HandoverDetailDto>;
    /// <summary>ما لم يكتمل قبل إغلاق المشروع: تنبيه لا منع</summary>
    public record GetClosureWarningsQuery(int ProjectId) : IRequest<ClosureWarningsDto>;

    public class RequestHandoverCommandValidator : AbstractValidator<RequestHandoverCommand>
    {
        public RequestHandoverCommandValidator() =>
            RuleFor(x => x.Dto.Kind).Must(HandoverRules.IsValidKind).WithMessage("نوع الاستلام: أولي أو نهائي");
    }

    public class SearchHandoversHandler(HandoverContext ctx) : IRequestHandler<SearchHandoversQuery, PagedResultDto<HandoverListItemDto>>
    {
        public async Task<PagedResultDto<HandoverListItemDto>> Handle(SearchHandoversQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!AppPermissions.AnyOf["AnyHandoverView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض محاضر الاستلام");
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var today = DateTime.Today;

            HandoverKind? kind = request.Kind is int k && HandoverRules.IsValidKind(k) ? (HandoverKind)k : null;
            HandoverStatus? status = request.Status is int s && Enum.IsDefined(typeof(HandoverStatus), s) ? (HandoverStatus)s : null;
            var filter = new HandoverFilter(request.ProjectId, request.BranchId, kind, status, request.Search, request.OpenNotesOnly, today);

            var (items, total) = await ctx.Handovers.SearchAsync(ProjectRules.Visible(viewer), filter, page, pageSize);
            return new PagedResultDto<HandoverListItemDto>
            {
                Items = items.Select(h => ToListItem(h, today)).ToList(), TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }

    public class GetHandoverHandler(HandoverContext ctx) : IRequestHandler<GetHandoverQuery, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(GetHandoverQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var record = await ctx.LoadAsync(viewer, request.Id);
            return ToDetail(record, viewer, DateTime.Today);
        }
    }

    public class RequestHandoverHandler(HandoverContext ctx) : IRequestHandler<RequestHandoverCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(RequestHandoverCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var project = await ctx.Projects.GetByIdAsync(dto.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            var kind = (HandoverKind)dto.Kind;
            var contract = dto.ContractId is int contractId ? await ctx.Contracts.GetByIdAsync(contractId) ?? throw new KeyNotFoundException("العقد غير موجود") : null;
            var existing = await ctx.Handovers.GetByProjectAsync(project.Id);
            HandoverRules.EnsureCanRequest(viewer, project, kind, contract, existing, dto.Note);
            var correspondence = await ctx.ResolveCorrespondenceAsync(viewer, dto.CorrespondenceId, project.BranchId);

            var record = await ctx.Handovers.AddAsync(new HandoverRecord
            {
                ProjectId = project.Id, ContractId = contract?.Id, Kind = kind, Status = HandoverStatus.Requested, RequestedByUserId = viewer.Id,
                RequestedAt = DateTime.Today, CorrespondenceId = correspondence?.Id, RequestNote = (dto.Note ?? string.Empty).Trim()
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Handover, null,
                $"طلب {HandoverRules.KindAr(kind)} على المشروع «{project.Name}»",
                Lines(contract is null ? null : $"العقد: {ContractRules.Number(contract.Id, contract.CreatedAt)}", correspondence is null ? null : "مرّ بمعاملة واردة")));

            var fresh = await ctx.Handovers.GetByIdAsync(record.Id) ?? record;
            await ctx.NotifyAsync(await ctx.HoldersAsync(project, AppPermissions.ManageHandover), viewer.Id, fresh, NotificationType.HandoverRequested,
                "طلب استلام بانتظار تشكيل لجنته", $"قدّم {viewer.User.FullName} طلب {HandoverRules.KindAr(kind)} على المشروع «{project.Name}»");
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    public class CancelHandoverHandler(HandoverContext ctx) : IRequestHandler<CancelHandoverCommand, HandoverDetailDto>
    {
        public async Task<HandoverDetailDto> Handle(CancelHandoverCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var record = await ctx.LoadAsync(viewer, request.Id);
            HandoverRules.EnsureCanCancel(viewer, record);
            if ((dto.Reason ?? string.Empty).Length > 1000) throw new InvalidOperationException("السبب لا يتجاوز 1000 حرف");

            record.Status = HandoverStatus.Cancelled;
            await ctx.Handovers.UpdateAsync(record, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Cancelled, AuditEntities.Handover, record.Id, $"إلغاء {HandoverContext.Label(record)}",
                    string.IsNullOrWhiteSpace(dto.Reason) ? string.Empty : $"السبب: {dto.Reason.Trim()}"));
            return await ctx.DetailAsync(viewer, record.Id);
        }
    }

    public class GetClosureWarningsHandler(HandoverContext ctx) : IRequestHandler<GetClosureWarningsQuery, ClosureWarningsDto>
    {
        public async Task<ClosureWarningsDto> Handle(GetClosureWarningsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var project = await ctx.Projects.GetByIdAsync(request.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            HandoverRules.EnsureCanView(viewer, project);
            var data = await ctx.Handovers.GetClosureDataAsync(project.Id);
            return new ClosureWarningsDto { ProjectId = project.Id, Warnings = HandoverRules.ClosureWarnings(data.Records, data.Contracts) };
        }
    }
}
