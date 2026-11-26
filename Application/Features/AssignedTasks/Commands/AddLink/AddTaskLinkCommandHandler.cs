using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks.Commands.AddLink
{
    public class AddTaskLinkCommandHandler : IRequestHandler<AddTaskLinkCommand, List<TaskLinkDto>>
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IAssignedTaskPlanningService _planning;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly TaskLinkResolver _resolver;
        private readonly TaskLinksSupport _support;

        public AddTaskLinkCommandHandler(IAssignedTaskService tasks, IAssignedTaskPlanningService planning, IUserService userService, IUserPermissionService permissions, TaskLinkResolver resolver, TaskLinksSupport support)
        {
            _tasks = tasks;
            _planning = planning;
            _userService = userService;
            _permissions = permissions;
            _resolver = resolver;
            _support = support;
        }

        public async Task<List<TaskLinkDto>> Handle(AddTaskLinkCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var task = await _support.ViewableTaskAsync(request.TaskId, viewer);
            if (task.CreatedByUserId != viewer.Id && !AssignedTaskRules.CanHandle(task, viewer) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("الربط للمُسنِد أو الجهة المنفِّذة");
            if (task.Status == AssignedTaskStatus.Done) throw new InvalidOperationException("لا يمكن الربط بمهمة منجزة");
            AssignedTaskRules.EnsureNotFrozen(task, viewer);
            if (!TaskLinkResolver.TryParseType(request.Dto.EntityType, out var type)) throw new ArgumentException("نوع السجل غير صحيح");
            if (await _planning.CountLinksAsync(task.Id) >= TaskPlanningRules.MaxLinksPerTask)
                throw new InvalidOperationException($"الحد الأقصى {TaskPlanningRules.MaxLinksPerTask} روابط للمهمة");

            var entityId = request.Dto.EntityId is > 0 ? request.Dto.EntityId.Value
                : TaskLinkResolver.ParseReference(request.Dto.Reference) ?? throw new ArgumentException("رقم السجل غير مفهوم");
            var described = await _resolver.DescribeAsync(viewer, type, entityId);
            if (!described.Exists || !described.Available)
                throw new KeyNotFoundException("السجل غير موجود أو لا تملك صلاحية عرضه");
            if (await _planning.LinkExistsAsync(task.Id, type, entityId)) throw new InvalidOperationException("هذا السجل مرتبط بالمهمة بالفعل");

            await _planning.AddLinkAsync(new AssignedTaskLink
            {
                AssignedTaskId = task.Id, EntityType = type, EntityId = entityId,
                CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow
            });
            await _tasks.AddActivityAsync(AssignedTaskRules.Activity(task, viewer.User, AssignedTaskActivityType.Edited,
                $"ربط المهمة بـ{TaskLinkResolver.TypeAr(type)}: {described.Label}"));
            return await _support.ListAsync(task, viewer);
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_userService, _permissions);
    }
}
