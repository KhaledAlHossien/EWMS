using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Submit
{
    public record SubmitStudyAssignmentCommand(int Id) : IRequest<StudyAssignmentDto>;
}
