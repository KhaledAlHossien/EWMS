using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetReport
{
    public class GetSparePartReportQueryHandler : IRequestHandler<GetSparePartReportQuery, SparePartReportDto>
    {
        private readonly ISparePartService _service;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetSparePartReportQueryHandler(ISparePartService service, IUserService userService, IUserPermissionService permissions)
        {
            _service = service;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<SparePartReportDto> Handle(GetSparePartReportQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var department = SparePartRules.Department(viewer) ?? request.DepartmentId;
            var to = request.To?.Date ?? DateTime.Today;
            var from = request.From?.Date ?? new DateTime(to.Year, to.Month, 1);
            return await _service.GetReportAsync(department, from, to);
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
