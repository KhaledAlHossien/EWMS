using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Queries.GetById
{
    public record GetStudyProjectQuery(int Id) : IRequest<StudyProjectDto>;
}
