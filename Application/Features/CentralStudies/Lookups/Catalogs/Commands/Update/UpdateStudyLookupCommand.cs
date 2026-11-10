using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Update
{
    public record UpdateStudyLookupCommand(StudyLookupKind Kind, int Id, StudyLookupFormDto Dto) : IRequest<StudyLookupDto>;
}
