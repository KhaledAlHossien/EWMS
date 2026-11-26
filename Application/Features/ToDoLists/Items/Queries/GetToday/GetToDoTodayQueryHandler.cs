using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using MediatR;

namespace Application.Features.ToDoLists.Items.Queries.GetToday
{
    public class GetToDoTodayQueryHandler : IRequestHandler<GetToDoTodayQuery, ToDoTodayDto>
    {
        private readonly IToDoListService _service;
        private readonly IMapper _mapper;
        private readonly ToDoItemSupport _support;

        public GetToDoTodayQueryHandler(IToDoListService service, IMapper mapper, ToDoItemSupport support)
        {
            _service = service;
            _mapper = mapper;
            _support = support;
        }

        public async Task<ToDoTodayDto> Handle(GetToDoTodayQuery request, CancellationToken ct)
        {
            var viewer = await _support.ViewerAsync();
            var today = DateTime.Today;
            var items = await _service.GetDueItemsAsync(viewer.Id, today);

            var dtos = items
                .OrderBy(i => i.DueDate).ThenByDescending(i => i.IsImportant).ThenBy(i => i.ToDoList.Name).ThenBy(i => i.SortOrder)
                .Select(i =>
                {
                    var dto = _mapper.Map<ToDoTodayItemDto>(i);
                    dto.ListName = i.ToDoList.Name; dto.ListColor = i.ToDoList.Color; dto.ListIcon = i.ToDoList.Icon;
                    if (i.LinkedTask != null) dto.LinkedTask = ToDoRules.LinkInfo(i.LinkedTask, viewer, today);
                    return dto;
                }).ToList();

            return new ToDoTodayDto
            {
                Items = dtos,
                OverdueCount = dtos.Count(d => d.IsOverdue),
                TodayCount = dtos.Count(d => !d.IsOverdue)
            };
        }
    }
}
