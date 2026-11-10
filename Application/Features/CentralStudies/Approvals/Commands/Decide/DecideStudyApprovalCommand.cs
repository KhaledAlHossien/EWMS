using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Commands.Decide
{
    public record DecideStudyApprovalCommand(int Id, StudyDecisionDto Dto) : IRequest<StudyApprovalDto>;
}
