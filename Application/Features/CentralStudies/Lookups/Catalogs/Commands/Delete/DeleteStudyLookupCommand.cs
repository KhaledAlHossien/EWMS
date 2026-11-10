using MediatR;

namespace Application.Features.CentralStudies.Lookups.Catalogs.Commands.Delete
{
    public record DeleteStudyLookupCommand(StudyLookupKind Kind, int Id) : IRequest<Unit>;
}
