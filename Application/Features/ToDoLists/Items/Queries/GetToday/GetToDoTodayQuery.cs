using Application.DTOs.Response;
using MediatR;

namespace Application.Features.ToDoLists.Items.Queries.GetToday
{
    /// <summary>«مهامي اليوم»: المتأخرة والمستحقة اليوم من كل قوائمي</summary>
    public record GetToDoTodayQuery : IRequest<ToDoTodayDto>;
}
