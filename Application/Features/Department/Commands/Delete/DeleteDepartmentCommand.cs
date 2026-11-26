using MediatR;

namespace Application.Features.Department.Commands.Delete
{
    public record DeleteDepartmentCommand(int Id) : IRequest<bool>;
}