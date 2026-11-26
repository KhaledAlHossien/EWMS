using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.SaveRecurrence
{
    public record SaveTaskRecurrenceCommand(int? Id, SaveTaskRecurrenceRequestDto Dto) : IRequest<TaskRecurrenceDto>;
}
