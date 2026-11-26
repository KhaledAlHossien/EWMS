using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.PreviewProjectCode
{
    /// <summary>AssignmentId: معاينة كود مسودة قائمة عند تغيير تاريخها (يُستعمل رقم الأرشيف المحفوظ في كودها)</summary>
    public record PreviewStudyProjectCodeQuery(int RequestId, DateTime AssignmentDate, string? AdminArchiveNumber, int? AssignmentId = null)
        : IRequest<string>;
}
