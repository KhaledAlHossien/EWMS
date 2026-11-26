using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.Update
{
    public class UpdateAssignedTaskCommandHandler : IRequestHandler<UpdateAssignedTaskCommand, AssignedTaskDetailDto>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;

        public UpdateAssignedTaskCommandHandler(IAssignedTaskService taskService, IUserService userService, IUserPermissionService permissions)
        {
            _taskService = taskService;
            _userService = userService;
            _permissions = permissions;
        }

        public async Task<AssignedTaskDetailDto> Handle(UpdateAssignedTaskCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var user = viewer.User;
            var task = await LoadAsync(request.Id);

            if (task.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("يعدّل المهمة من أسندها فقط");
            if (task.Status == AssignedTaskStatus.Done)
                throw new InvalidOperationException("لا يمكن تعديل مهمة منجزة");

            var dto = request.Dto;
            task.Title = dto.Title.Trim();
            task.Description = dto.Description.Trim();
            task.Priority = (AssignedTaskPriority)dto.Priority;
            // موعد جديد = تذكيرات جديدة (تُرسل مرة واحدة لكل موعد)
            if (task.DueDate?.Date != dto.DueDate?.Date) { task.DueSoonNotifiedAt = null; task.OverdueNotifiedAt = null; }
            task.DueDate = dto.DueDate?.Date;
            task.UpdatedAt = DateTime.UtcNow;
            await _taskService.SaveChangesAsync();
            await _taskService.AddActivityAsync(AssignedTaskRules.Activity(task, user, AssignedTaskActivityType.Edited, "عدّل تفاصيل المهمة"));

            return AssignedTaskRules.ToDetail(await LoadAsync(task.Id), viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);

        private async Task<AssignedTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
    }
}
