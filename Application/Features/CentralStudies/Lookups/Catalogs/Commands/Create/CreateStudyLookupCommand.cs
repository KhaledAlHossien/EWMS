using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Create
{
    public record CreateStudyLookupCommand(StudyLookupKind Kind, StudyLookupFormDto Dto) : IRequest<StudyLookupDto>;
}
