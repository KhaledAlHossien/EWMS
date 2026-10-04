using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Common;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using MediatR;

namespace Application.Features.Maintenance.Requests
{
    /// <summary>بحث/عرض الطلبات ضمن نطاقي (طلباتي، أو طلبات قسمي لرئيس القسم، أو فرعي لرئيس الفرع، أو الكل للسوبر ادمن)</summary>
    public record SearchMaintenanceRequestsQuery(MaintenanceRequestFilterDto Filter)
        : IRequest<PagedResultDto<MaintenanceRequestResponseDto>>;

    public record GetMaintenanceRequestByIdQuery(int Id) : IRequest<MaintenanceRequestResponseDto>;

    /// <summary>الفنيون الذين لهم طلبات ضمن نطاقي (لقائمة فلتر "الفني")</summary>
    public record GetMaintenanceTechniciansQuery : IRequest<List<TechnicianOptionDto>>;

    /// <summary>سجل الطلب (الأحدث أولاً)</summary>
    public record GetMaintenanceRequestActivitiesQuery(int Id) : IRequest<List<MaintenanceActivityDto>>;

    /// <summary>بيانات الطباعة: الطلب + اسم وتوقيع من يملك SignMaintenanceReceipt في قسم الطلب</summary>
    public record GetMaintenanceRequestPrintQuery(int Id) : IRequest<MaintenancePrintDto>;

    /// <summary>إحصائيات الصيانة ضمن نطاقي (طلبات + مهام)</summary>
    public record GetMaintenanceStatsQuery : IRequest<MaintenanceStatsDto>;

    /// <summary>
    /// الموظفون الذين يمكن نقل طلب/مهمة إليهم: موظفو قسم رئيس القسم،
    /// وللسوبر ادمن موظفو القسم المحدد (أو كل من له قسم).
    /// </summary>
    public record GetMaintenanceAssigneesQuery(int? DepartmentId) : IRequest<List<TechnicianOptionDto>>;

    public class MaintenanceRequestQueriesHandler :
        IRequestHandler<SearchMaintenanceRequestsQuery, PagedResultDto<MaintenanceRequestResponseDto>>,
        IRequestHandler<GetMaintenanceRequestByIdQuery, MaintenanceRequestResponseDto>,
        IRequestHandler<GetMaintenanceTechniciansQuery, List<TechnicianOptionDto>>,
        IRequestHandler<GetMaintenanceRequestActivitiesQuery, List<MaintenanceActivityDto>>,
        IRequestHandler<GetMaintenanceRequestPrintQuery, MaintenancePrintDto>,
        IRequestHandler<GetMaintenanceStatsQuery, MaintenanceStatsDto>,
        IRequestHandler<GetMaintenanceAssigneesQuery, List<TechnicianOptionDto>>
    {
        private readonly IMaintenanceRequestService _requestService;
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserSignatureService _signatureService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IMapper _mapper;

        public MaintenanceRequestQueriesHandler(
            IMaintenanceRequestService requestService,
            IMaintenanceTaskService taskService,
            IUserSignatureService signatureService,
            IUserService userService,
            IUserPermissionService permissions,
            IMapper mapper)
        {
            _permissions = permissions;
            _requestService = requestService;
            _taskService = taskService;
            _signatureService = signatureService;
            _userService = userService;
            _mapper = mapper;
        }

        private async Task<(MaintenanceRequest Entity, MaintenanceRules.Scopes Scopes)> LoadForViewAsync(int id)
        {
            var scopes = await MaintenanceRules.RequestScopesAsync(_userService, _permissions);
            var entity = await _requestService.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("طلب الصيانة غير موجود");

            MaintenanceRules.Ensure(MaintenanceRules.In(scopes.View, entity), "لا يمكنك عرض طلب صيانة خارج نطاقك");

            return (entity, scopes);
        }

        public async Task<PagedResultDto<MaintenanceRequestResponseDto>> Handle(
            SearchMaintenanceRequestsQuery request, CancellationToken ct)
        {
            var scopes = await MaintenanceRules.RequestScopesAsync(_userService, _permissions);
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Filter.Page, request.Filter.PageSize);

            var (items, total) = await _requestService.SearchAsync(
                MaintenanceRules.RequestFilter(scopes.View), request.Filter, page, pageSize);

            return new PagedResultDto<MaintenanceRequestResponseDto>
            {
                Items = items.Select(r => MaintenanceRequestMapping.ToDto(_mapper, r, scopes)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MaintenanceRequestResponseDto> Handle(GetMaintenanceRequestByIdQuery request, CancellationToken ct)
        {
            var (entity, scopes) = await LoadForViewAsync(request.Id);
            return MaintenanceRequestMapping.ToDto(_mapper, entity, scopes);
        }

        public async Task<List<TechnicianOptionDto>> Handle(GetMaintenanceTechniciansQuery request, CancellationToken ct)
        {
            var view = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "ViewMaintenanceRequests");
            return await _requestService.GetTechniciansAsync(MaintenanceRules.RequestFilter(view));
        }

        public async Task<List<MaintenanceActivityDto>> Handle(GetMaintenanceRequestActivitiesQuery request, CancellationToken ct)
        {
            await LoadForViewAsync(request.Id);
            return _mapper.Map<List<MaintenanceActivityDto>>(await _requestService.GetActivitiesAsync(request.Id));
        }

        public async Task<MaintenancePrintDto> Handle(GetMaintenanceRequestPrintQuery request, CancellationToken ct)
        {
            var (entity, scopes) = await LoadForViewAsync(request.Id);

            var result = new MaintenancePrintDto { Request = MaintenanceRequestMapping.ToDto(_mapper, entity, scopes) };

            if (entity.DeliveredAt != null)
            {
                // سُلِّم: الموقّع وتوقيعه كما ثُبِّتا لحظة التسليم (لا يتغيران بعد ذلك)
                result.Delivered = true;
                result.DeliveredAt = entity.DeliveredAt;
                result.ManagerName = entity.DeliverySigner?.FullName ?? string.Empty;
                result.ManagerSignature = await _signatureService.GetImageAsync(entity.DeliverySignatureId);
            }
            else if (entity.DepartmentId is int departmentId)
            {
                // لم يُسلَّم بعد: اسم الموقّع المتوقع فقط، بلا توقيع (يُثبَّت عند تحويل الطلب إلى حالة تسليم)
                var manager = (await _permissions.GetUsersWithPermissionAsync(AppPermissions.SignMaintenanceReceipt, departmentId: departmentId))
                    .OrderBy(u => u.Id)
                    .FirstOrDefault();
                result.ManagerName = manager?.FullName ?? string.Empty;
            }

            return result;
        }

        public async Task<MaintenanceStatsDto> Handle(GetMaintenanceStatsQuery request, CancellationToken ct)
        {
            var stats = await _requestService.GetStatsAsync(
                MaintenanceRules.RequestFilter(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ViewMaintenanceStats)));
            await _taskService.FillStatsAsync(
                MaintenanceRules.TaskFilter(await MaintenanceRules.BoundaryAsync(_userService, _permissions, AppPermissions.ViewMaintenanceStats)), stats);
            return stats;
        }

        public async Task<List<TechnicianOptionDto>> Handle(GetMaintenanceAssigneesQuery request, CancellationToken ct)
        {
            // من يملك نقل الطلبات أو المهام: موظفو قسمه (SuperAdmin: القسم المحدد أو الكل)
            var assign = await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceRequest")
                ?? await MaintenanceRules.BoundaryAsync(_userService, _permissions, "AssignMaintenanceTask");

            var users = await MaintenanceRules.AssigneesAsync(_userService, assign, request.DepartmentId);

            return users
                .OrderBy(u => u.FullName)
                .Select(u => new TechnicianOptionDto { Id = u.Id, FullName = u.FullName })
                .ToList();
        }
    }
}
