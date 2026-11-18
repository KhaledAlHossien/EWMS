using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.PreviewProjectCode
{
    public record PreviewStudyProjectCodeQuery(int RequestId, DateTime AssignmentDate, string? AdminArchiveNumber) : IRequest<string>;

    public class PreviewStudyProjectCodeQueryHandler(
        IStudyRequestService requests, StudyKit kit, StudyAssignmentSupport support)
        : IRequestHandler<PreviewStudyProjectCodeQuery, string>
    {
        public async Task<string> Handle(PreviewStudyProjectCodeQuery query, CancellationToken ct)
        {
            var viewer = await kit.ViewerAsync();
            var request = await requests.GetByIdAsync(query.RequestId)
                ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, request.BranchId);
            StudyRules.EnsureApproved(request.Status, "الطلب");
            return await support.NewProjectCodeAsync(request, query.AssignmentDate, query.AdminArchiveNumber);
        }
    }
}
