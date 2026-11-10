using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Queries.GetByOwner
{
    public record GetStudyApprovalsQuery(int OwnerType, int OwnerId) : IRequest<List<StudyApprovalDto>>;
}
