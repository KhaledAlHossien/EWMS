using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Queries.GetById
{
    public record GetStudyAssignmentQuery(int Id) : IRequest<StudyAssignmentDto>;
}
