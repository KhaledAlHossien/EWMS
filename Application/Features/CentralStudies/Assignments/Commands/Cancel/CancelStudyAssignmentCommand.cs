using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Cancel
{
    public record CancelStudyAssignmentCommand(int Id, string Reason) : IRequest<StudyAssignmentDto>;
}
