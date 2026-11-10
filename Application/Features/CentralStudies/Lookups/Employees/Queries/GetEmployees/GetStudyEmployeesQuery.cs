using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Employees.Queries.GetEmployees
{
    /// <summary>
    /// موظفو فرع الدراسة النشطون لقوائم الاختيار (بلا ViewUsers وبلا بيانات حساسة). الحد فرع المستخدم (StructureScope)؛
    /// من يتصفح المؤسسة يمرّر branchId أو departmentId. قسم أو فرع خارج الحد يُرفض.
    /// </summary>
    public record GetStudyEmployeesQuery(int? BranchId, int? DepartmentId) : IRequest<List<StudyEmployeeDto>>;
}
