using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.Reorder
{
    public class ReorderToDoItemsCommandHandler : IRequestHandler<ReorderToDoItemsCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly ToDoItemSupport _support;

        public ReorderToDoItemsCommandHandler(IToDoListService service, ToDoItemSupport support)
        {
            _service = service;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(ReorderToDoItemsCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId);

            // الترتيب الجديد يجب أن يضم كل بنود القائمة مرة واحدة: لا ناقص ولا زائد ولا مكرر ولا من قائمة أخرى
            var ids = request.ItemIds;
            if (ids.Count != list.Items.Count || ids.Distinct().Count() != ids.Count || !list.Items.All(i => ids.Contains(i.Id)))
                throw new InvalidOperationException("الترتيب لا يطابق بنود القائمة — حدّث الصفحة وأعد المحاولة");

            var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            foreach (var item in list.Items) item.SortOrder = order[item.Id];
            await _service.SaveChangesAsync();
            return await _support.ResultAsync(list.Id, viewer);
        }
    }
}
