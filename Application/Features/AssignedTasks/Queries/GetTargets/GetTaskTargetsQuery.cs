using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetTargets
{
    /// <summary>
    /// الجهات التي يستطيع المستخدم الإسناد إليها من نوع معيّن (أقسام فرعه / مكاتب قسمه / موظفو مكتبه)،
    /// أو جهات التفويض من مهمة واردة (مكاتب قسم المهمة / موظفو مكتبها).
    /// </summary>
    public record GetTaskTargetsQuery(string? Type = null, int? ParentTaskId = null) : IRequest<List<TaskTargetOptionDto>>;
}
