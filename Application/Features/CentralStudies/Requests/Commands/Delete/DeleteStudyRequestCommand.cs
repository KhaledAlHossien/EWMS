using MediatR;

namespace Application.Features.CentralStudies.Requests.Commands.Delete
{
    public record DeleteStudyRequestCommand(int Id) : IRequest<Unit>;
}
