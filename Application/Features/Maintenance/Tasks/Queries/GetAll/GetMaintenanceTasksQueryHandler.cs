using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Tasks.Queries.GetAll
{
    public class GetMaintenanceTasksQueryHandler : IRequestHandler<GetMaintenanceTasksQuery, PagedResultDto<MaintenanceTaskResponseDto>>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly MaintenanceTaskSupport _support;

        public GetMaintenanceTasksQueryHandler(IMaintenanceTaskService taskService, IUserService userService, IUserPermissionService permissions, MaintenanceTaskSupport support)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
            _support = support;
        }

        public async Task<PagedResultDto<MaintenanceTaskResponseDto>> Handle(GetMaintenanceTasksQuery request, CancellationToken ct)
        {
            var scopes = await MaintenanceRules.TaskScopesAsync(_userService, _permissions);
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Page, request.PageSize);

            var (items, total) = await _taskService.GetPageAsync(
                MaintenanceRules.TaskFilter(scopes.View), request.UserId, page, pageSize);

            return new PagedResultDto<MaintenanceTaskResponseDto>
            {
                Items = items.Select(t => _support.ToDto(t, scopes)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
