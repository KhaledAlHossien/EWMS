using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.FindClient
{
    public class FindMaintenanceClientQueryHandler : IRequestHandler<FindMaintenanceClientQuery, List<MaintenanceClientDto>>
    {
        private readonly IUserService _userService;

        public FindMaintenanceClientQueryHandler(IUserService userService)
        {
            _userService = userService;
        }

        public async Task<List<MaintenanceClientDto>> Handle(FindMaintenanceClientQuery request, CancellationToken ct) =>
            (await MaintenanceClients.FindAsync(_userService, request.Query))
                .Select(u => new MaintenanceClientDto { Id = u.Id, FullName = u.FullName, DepartmentName = u.Department?.Name ?? "", Phone = u.PhoneNumber })
                .ToList();
    }
}
