using Application.DTOs.Response;
using MediatR;

namespace Application.Features.AssignedTasks.Queries.GetBoard
{
    /// <summary>
    /// Mode: incoming (الواردة — أنا المنفِّذ)، outgoing (الصادرة — أنا المُسنِد)، scope (كل مهام نطاقي).
    /// المنجزة تُعرض لآخر 30 يوماً فقط حتى لا تزدحم اللوحة.
    /// </summary>
    public record GetTaskBoardQuery(string Mode, int? DoneDays = null) : IRequest<TaskBoardDto>;
}
