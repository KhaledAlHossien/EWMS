using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Employees.Queries.GetEmployees
{
    public class GetStudyEmployeesQueryHandler : IRequestHandler<GetStudyEmployeesQuery, List<StudyEmployeeDto>>
    {
        private readonly IStudyLookupService _service;
        private readonly IDepartmentService _departments;
        private readonly StudyKit _kit;

        public GetStudyEmployeesQueryHandler(IStudyLookupService service, IDepartmentService departments, StudyKit kit)
        {
            _service = service;
            _departments = departments;
            _kit = kit;
        }

        // ----- موظفو فرع الدراسة -----
        public async Task<List<StudyEmployeeDto>> Handle(GetStudyEmployeesQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            int? departmentBranch = null;
            if (request.DepartmentId is int departmentId)
            {
                var department = await _departments.GetByIdAsync(departmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
                StudyRules.EnsureInScope(viewer, department.BranchId);
                departmentBranch = department.BranchId;
            }

            // بلا فرع ولا قسم: فرع المستخدم؛ ومن يتصفح المؤسسة يحدد الفرع
            var branchId = request.BranchId ?? departmentBranch ?? StudyRules.BranchLimit(viewer)
                ?? throw new InvalidOperationException("حدد الفرع أو القسم");
            if (branchId == 0) return [];   // مستخدم بلا فرع وبلا نظرة مؤسسة لا يرى أحداً
            StudyRules.EnsureInScope(viewer, branchId);
            if (departmentBranch != null && departmentBranch != branchId)
                throw new InvalidOperationException("القسم ليس من أقسام الفرع المحدد");

            return await _service.GetEmployeesAsync(branchId, request.DepartmentId);
        }
    }
}
