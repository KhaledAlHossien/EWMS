using Application.Common;
using Application.DTOs.Response;
using Application.Features.AssignedTasks;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.ToDoLists.Items.Commands.AddFromTask
{
    public class AddTaskToToDoListCommandHandler : IRequestHandler<AddTaskToToDoListCommand, ToDoListResponseDto>
    {
        private readonly IToDoListService _service;
        private readonly IAssignedTaskService _tasks;
        private readonly ToDoItemSupport _support;

        public AddTaskToToDoListCommandHandler(IToDoListService service, IAssignedTaskService tasks, ToDoItemSupport support)
        {
            _service = service;
            _tasks = tasks;
            _support = support;
        }

        public async Task<ToDoListResponseDto> Handle(AddTaskToToDoListCommand request, CancellationToken ct)
        {
            var (viewer, list) = await _support.LoadListAsync(request.ListId);
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ViewTaskBoard))
                throw new UnauthorizedAccessException("لا تملك صلاحية لوحة المهام");

            var task = await _tasks.GetByIdAsync(request.TaskId) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            if (!AssignedTaskRules.CanView(task, viewer)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
            if (list.Items.Any(i => i.LinkedTaskId == task.Id)) throw new InvalidOperationException("هذه المهمة مضافة إلى القائمة بالفعل");
            if (list.Items.Count >= ToDoRules.MaxItemsPerList)
                throw new InvalidOperationException($"الحد الأقصى {ToDoRules.MaxItemsPerList} بنداً للقائمة");

            await _service.AddItemAsync(new ToDoItem
            {
                ToDoListId = list.Id,
                Title = task.Title.Length > ToDoRules.MaxTitleLength ? task.Title[..ToDoRules.MaxTitleLength] : task.Title,
                DueDate = task.DueDate?.Date,
                LinkedTaskId = task.Id,
                SortOrder = await _service.FirstSortOrderAsync(list.Id),
                CreatedAt = DateTime.UtcNow
            });
            return await _support.ResultAsync(list.Id, viewer);
        }
    }
}
