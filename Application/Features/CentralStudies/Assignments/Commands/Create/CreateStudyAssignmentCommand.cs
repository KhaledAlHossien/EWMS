using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Create
{
    public record CreateStudyAssignmentCommand(StudyAssignmentFormDto Dto) : IRequest<StudyAssignmentDto>;
}
