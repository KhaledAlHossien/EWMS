using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Queries.GetAll
{
    public record GetStudyLookupsQuery(StudyLookupKind Kind, bool ActiveOnly) : IRequest<List<StudyLookupDto>>;
}
