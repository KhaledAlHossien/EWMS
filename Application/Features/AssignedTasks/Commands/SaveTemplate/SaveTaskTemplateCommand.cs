using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.SaveTemplate
{
    public record SaveTaskTemplateCommand(int? Id, SaveTaskTemplateRequestDto Dto) : IRequest<TaskTemplateDto>;
}
