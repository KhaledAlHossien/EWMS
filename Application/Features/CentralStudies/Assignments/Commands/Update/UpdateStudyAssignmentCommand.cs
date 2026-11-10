using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Update
{
    public record UpdateStudyAssignmentCommand(int Id, StudyAssignmentFormDto Dto) : IRequest<StudyAssignmentDto>;
}
