using MediatR;

namespace Application.Features.Department.Command.Delete
{
    public record DeleteDepartmentCommand(int Id) : IRequest<bool>;
}