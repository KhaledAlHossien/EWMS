using MediatR;

namespace Application.Features.Offices.Commands.Delete
{
    public record DeleteOfficeCommand(int Id) : IRequest<bool>;
}
