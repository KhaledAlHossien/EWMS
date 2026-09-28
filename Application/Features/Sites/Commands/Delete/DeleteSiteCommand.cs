using MediatR;

namespace Application.Features.Sites.Commands.Delete
{
    public record DeleteSiteCommand(int Id) : IRequest<bool>;
}
