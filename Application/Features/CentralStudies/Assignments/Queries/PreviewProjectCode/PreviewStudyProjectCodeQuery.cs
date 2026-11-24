using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.PreviewProjectCode
{
    /// <summary>AssignmentId: معاينة كود مسودة قائمة عند تغيير تاريخها (يُستعمل رقم الأرشيف المحفوظ في كودها)</summary>
    public record PreviewStudyProjectCodeQuery(int RequestId, DateTime AssignmentDate, string? AdminArchiveNumber, int? AssignmentId = null)
        : IRequest<string>;

    public class PreviewStudyProjectCodeQueryHandler(
        IStudyRequestService requests, StudyKit kit, StudyAssignmentSupport support)
        : IRequestHandler<PreviewStudyProjectCodeQuery, string>
    {
        public async Task<string> Handle(PreviewStudyProjectCodeQuery query, CancellationToken ct)
        {
            var viewer = await kit.ViewerAsync();
            if (query.AssignmentId is int assignmentId)
            {
                StudyRules.Require(viewer, StudyPermissions.EditAssignment);
                var assignment = await support.LoadAsync(assignmentId, viewer);
                if (assignment.RequestId != query.RequestId)
                    throw new InvalidOperationException("التكليف لا يتبع هذا الطلب");
                return await support.RefreshProjectCodeAsync(assignment, query.AssignmentDate, query.AdminArchiveNumber)
                    ?? string.Empty;
            }

            StudyRules.Require(viewer, "CreateStudyAssignment");
            var request = await requests.GetByIdAsync(query.RequestId)
                ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, request.BranchId);
            StudyRules.EnsureApproved(request.Status, "الطلب");
            return await support.NewProjectCodeAsync(request, query.AssignmentDate, query.AdminArchiveNumber);
        }
    }
}
