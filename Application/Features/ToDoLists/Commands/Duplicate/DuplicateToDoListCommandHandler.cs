using Application.DTOs.Response;
using Application.Features.ToDoLists.Items;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.ToDoLists.Commands.Duplicate
{
    public class DuplicateToDoListCommandHandler : IRequestHandler<DuplicateToDoListCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public DuplicateToDoListCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        /// <summary>نسخة من القائمة ببنودها غير منجزة وبلا مواعيد ولا تكرار ولا روابط (قالب جاهز لدورة جديدة)</summary>
        public async Task<ToDoListResponseDto> Handle(DuplicateToDoListCommand request, CancellationToken ct)
        {
            var (viewer, source) = await _support.LoadListAsync(request.ListId, forEdit: false);

            var baseName = source.Name.Length > 180 ? source.Name[..180] : source.Name;
            var name = $"{baseName} (نسخة)";
            for (var n = 2; await _service.ExistsByNameAsync(source.UserId, name); n++) name = $"{baseName} (نسخة {n})";

            var now = DateTime.UtcNow;
            var copy = new ToDoList
            {
                UserId = source.UserId, Name = name, Description = source.Description, Color = source.Color, Icon = source.Icon,
                Items = source.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select((i, index) => new ToDoItem
                {
                    Title = i.Title, Note = i.Note, IsImportant = i.IsImportant, Color = i.Color, SortOrder = index, CreatedAt = now
                }).ToList()
            };
            var created = await _service.AddAsync(copy);
            return await _support.ResultAsync(created.Id, viewer);
        }
    }
}
