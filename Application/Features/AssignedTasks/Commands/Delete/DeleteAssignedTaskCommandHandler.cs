using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Delete
{
    public class DeleteAssignedTaskCommandHandler : IRequestHandler<DeleteAssignedTaskCommand, Unit>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public DeleteAssignedTaskCommandHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<Unit> Handle(DeleteAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.Id);

            if (task.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("يحذف المهمة من أسندها فقط");
            if (task.Status != AssignedTaskStatus.Todo)
                throw new InvalidOperationException("لا يمكن حذف مهمة بدأ تنفيذها");
            if (task.SubTasks.Count > 0)
                throw new InvalidOperationException("لا يمكن حذف مهمة لها مهام فرعية");

            await _taskService.DeleteAsync(task);
            return Unit.Value;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
