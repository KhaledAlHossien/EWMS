using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Queries.GetDepartments
{
    public class GetSparePartDepartmentsQueryHandler : IRequestHandler<GetSparePartDepartmentsQuery, List<NamedRefDto>>
    {
        private readonly IDepartmentService _departmentService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public GetSparePartDepartmentsQueryHandler(IDepartmentService departmentService, IUserService userService, IUserPermissionService permissions)
        {
            _departmentService = departmentService;
            _userService = userService;
            _permissions = permissions;
        }

        /// <summary>الأقسام التي يدير المستخدم مخزونها: قسمه، ومدير النظام كل الأقسام (لاختيار القسم في الواجهة)</summary>
        public async Task<List<NamedRefDto>> Handle(GetSparePartDepartmentsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            if (viewer.IsSuperAdmin)
                return (await _departmentService.GetAllAsync())
                    .Select(d => new NamedRefDto { Id = d.Id, Name = d.Branch != null ? $"{d.Name} — {d.Branch.Name}" : d.Name })
                    .OrderBy(d => d.Name).ToList();

            if (viewer.User.DepartmentId is not int own) return [];
            var department = await _departmentService.GetByIdAsync(own);
            return department == null ? [] : [new NamedRefDto { Id = department.Id, Name = department.Name }];
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
