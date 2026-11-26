using Application.DTOs.Response;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Queries.GetColleagues
{
    public class GetTransferColleaguesQueryHandler : IRequestHandler<GetTransferColleaguesQuery, List<TechnicianOptionDto>>
    {
        private readonly IUserService _users;

        public GetTransferColleaguesQueryHandler(IUserService users)
        {
            _users = users;
        }

        public async Task<List<TechnicianOptionDto>> Handle(GetTransferColleaguesQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_users);
            if (user.DepartmentId is not int departmentId) return [];
            return (await _users.GetByDepartmentAsync(departmentId))
                .Where(u => u.IsActive && u.Id != user.Id)
                .OrderBy(u => u.FullName)
                .Select(u => new TechnicianOptionDto { Id = u.Id, FullName = u.FullName })
                .ToList();
        }
    }
}
