using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.AddComment
{
    public record AddAssignedTaskCommentCommand(int Id, string Text) : IRequest<AssignedTaskDetailDto>;
}
