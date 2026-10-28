using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    // قراءة المعاملات: البحث ضمن حدّ الرؤية، والتفصيل، وأثر العمليات، وخيارات الإحالة.

    /// <param name="Inbox">true = ما تحمله وحدتي الآن ولم يُغلق (صندوقي الوارد)</param>
    public record SearchCorrespondenceQuery(int? Direction, int? BranchId, int? TypeId, int? StatusId, int? Stage, int? ProjectId, string? Search,
        DateTime? From, DateTime? To, bool OverdueOnly, bool Inbox, int Page, int PageSize) : IRequest<PagedResultDto<CorrespondenceListItemDto>>;
    public record GetCorrespondenceQuery(int Id) : IRequest<CorrespondenceResponseDto>;
    public record GetCorrespondenceHistoryQuery(int Id) : IRequest<List<CorrespondenceHistoryDto>>;
    public record GetReferralTargetsQuery(int? BranchId) : IRequest<List<ReferralDepartmentDto>>;
    public record GetReferralUsersQuery(int DepartmentId, int? OfficeId) : IRequest<List<ReferralUserDto>>;

    public class SearchCorrespondenceHandler(DiwanContext ctx) : IRequestHandler<SearchCorrespondenceQuery, PagedResultDto<CorrespondenceListItemDto>>
    {
        public async Task<PagedResultDto<CorrespondenceListItemDto>> Handle(SearchCorrespondenceQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var today = DateTime.Today;

            CorrespondenceDirection? direction = request.Direction is int d && Enum.IsDefined(typeof(CorrespondenceDirection), d) ? (CorrespondenceDirection)d : null;
            CorrespondenceStage? stage = request.Stage is int s && DiwanRules.IsValidStage(s) ? (CorrespondenceStage)s : null;
            var filter = new DiwanFilter(direction, request.BranchId, request.TypeId, request.StatusId, stage, request.ProjectId, request.Search,
                request.From, request.To, request.OverdueOnly, request.Inbox ? DiwanRules.Inbox(viewer) : null, today);

            var (items, total) = await ctx.Diwan.SearchAsync(DiwanRules.Visible(viewer), filter, page, pageSize);
            return new PagedResultDto<CorrespondenceListItemDto>
            {
                Items = items.Select(c => ToListItem(c, today)).ToList(),
                TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }

    public class GetCorrespondenceHandler(DiwanContext ctx) : IRequestHandler<GetCorrespondenceQuery, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(GetCorrespondenceQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            return ToDetail(c, viewer, DateTime.Today);
        }
    }

    public class GetCorrespondenceHistoryHandler(DiwanContext ctx) : IRequestHandler<GetCorrespondenceHistoryQuery, List<CorrespondenceHistoryDto>>
    {
        public async Task<List<CorrespondenceHistoryDto>> Handle(GetCorrespondenceHistoryQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            await ctx.LoadAsync(viewer, request.Id);   // الحدّ على المعاملة نفسها

            var rows = await ctx.Diwan.GetHistoryAsync(request.Id);
            return rows.Select(a => new CorrespondenceHistoryDto
            {
                Id = a.Id, At = a.CreatedAt, UserName = a.User?.FullName ?? string.Empty, Action = a.Action, Title = a.Title, Details = a.Details
            }).ToList();
        }
    }

    public class GetReferralTargetsHandler(DiwanContext ctx) : IRequestHandler<GetReferralTargetsQuery, List<ReferralDepartmentDto>>
    {
        public async Task<List<ReferralDepartmentDto>> Handle(GetReferralTargetsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = DiwanContext.ResolveBranch(viewer, request.BranchId);
            DiwanContext.EnsureBranchAccess(viewer, branch);

            var result = new List<ReferralDepartmentDto>();
            foreach (var department in (await ctx.Departments.GetByBranchAsync(branch)).OrderBy(d => d.Name))
            {
                var offices = await ctx.Offices.GetByDepartmentAsync(department.Id);
                result.Add(new ReferralDepartmentDto
                {
                    Id = department.Id, Name = department.Name,
                    Offices = offices.OrderBy(o => o.Name).Select(o => new ReferralOfficeDto { Id = o.Id, Name = o.Name }).ToList()
                });
            }
            return result;
        }
    }

    public class GetReferralUsersHandler(DiwanContext ctx) : IRequestHandler<GetReferralUsersQuery, List<ReferralUserDto>>
    {
        public async Task<List<ReferralUserDto>> Handle(GetReferralUsersQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var department = await ctx.Departments.GetByIdAsync(request.DepartmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
            DiwanContext.EnsureBranchAccess(viewer, department.BranchId);

            // من يملك المعالجة في القسم (أو في مكتبه إن حُدّد)
            var holders = request.OfficeId is int office
                ? await ctx.Permissions.GetUsersWithPermissionAsync(AppPermissions.HandleCorrespondence, officeId: office)
                : await ctx.Permissions.GetUsersWithPermissionAsync(AppPermissions.HandleCorrespondence, departmentId: department.Id);
            return holders.OrderBy(u => u.FullName).Select(u => new ReferralUserDto { Id = u.Id, FullName = u.FullName, OfficeName = u.Office?.Name }).ToList();
        }
    }
}
