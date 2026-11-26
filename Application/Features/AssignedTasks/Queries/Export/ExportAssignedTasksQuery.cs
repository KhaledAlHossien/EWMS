using Application.DTOs.Request;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.Export
{
    /// <summary>تصدير مهام العرض الحالي (بنفس فلاتر الواجهة) إلى Excel</summary>
    public record ExportAssignedTasksQuery(AssignedTaskExportFilterDto Filter) : IRequest<byte[]>;
}
