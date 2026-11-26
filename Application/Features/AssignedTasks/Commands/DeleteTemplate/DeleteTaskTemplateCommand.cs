using MediatR;

namespace Application.Features.AssignedTasks.Commands.DeleteTemplate
{
    public record DeleteTaskTemplateCommand(int Id) : IRequest<Unit>;
}
