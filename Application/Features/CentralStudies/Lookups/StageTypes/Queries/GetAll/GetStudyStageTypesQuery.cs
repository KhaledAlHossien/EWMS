using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Queries.GetAll
{
    public record GetStudyStageTypesQuery(bool ActiveOnly) : IRequest<List<StudyStageTypeDto>>;
}
