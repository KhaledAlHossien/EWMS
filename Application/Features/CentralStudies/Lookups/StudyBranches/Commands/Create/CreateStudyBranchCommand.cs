using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Create
{
    public record CreateStudyBranchCommand(StudyBranchFormDto Dto) : IRequest<StudyBranchDto>;
}
