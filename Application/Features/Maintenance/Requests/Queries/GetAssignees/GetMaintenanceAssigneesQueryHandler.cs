using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.GetAssignees
{
    public class GetMaintenanceAssigneesQueryHandler : IRequestHandler<GetMaintenanceAssigneesQuery, List<TechnicianOptionDto>>
    {
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetMaintenanceAssigneesQueryHandler(IUserService userService, IUserPermissionService permissions)
        {
            _userService = userService;
            _permissions = permissions;
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
