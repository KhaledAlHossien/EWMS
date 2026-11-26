using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.RunRecurrenceNow
{
    /// <summary>ينشئ مهمة من التكرار الآن (دفعة إضافية)، ولا يغيّر موعده التالي</summary>
    public record RunTaskRecurrenceNowCommand(int Id) : IRequest<AssignedTaskDetailDto>;
}
