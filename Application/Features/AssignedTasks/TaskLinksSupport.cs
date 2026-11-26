using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class TaskLinksSupport
    {
        private readonly IAssignedTaskService _tasks;
        private readonly IAssignedTaskPlanningService _planning;
        private readonly TaskLinkResolver _resolver;

        public TaskLinksSupport(IAssignedTaskService tasks, IAssignedTaskPlanningService planning, TaskLinkResolver resolver)
        {
            _tasks = tasks;
            _planning = planning;
            _resolver = resolver;
        }

        public async Task<AssignedTask> ViewableTaskAsync(int taskId, Viewer viewer)
        {
            var task = await _tasks.GetByIdAsync(taskId) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            if (!AssignedTaskRules.CanView(task, viewer)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
            return task;
        }

        public async Task<List<TaskLinkDto>> ListAsync(AssignedTask task, Viewer viewer)
        {
            var list = new List<TaskLinkDto>();
            foreach (var l in await _planning.GetLinksAsync(task.Id))
            {
                var d = await _resolver.DescribeAsync(viewer, l.EntityType, l.EntityId);
                list.Add(new TaskLinkDto
                {
                    Id = l.Id,
                    EntityType = l.EntityType.ToString(),
                    EntityTypeAr = TaskLinkResolver.TypeAr(l.EntityType),
                    EntityId = l.EntityId,
                    Available = d.Available,
                    Label = d.Available ? d.Label : "سجل غير متاح لك",
                    CanRemove = !task.Status.Equals(AssignedTaskStatus.Done) && !AssignedTaskRules.IsFrozenFor(task, viewer)
                        && (l.CreatedByUserId == viewer.Id || task.CreatedByUserId == viewer.Id || viewer.IsSuperAdmin),
                    CreatedByName = l.CreatedByUser?.FullName ?? string.Empty
                });
            }
            return list;
        }
    }
}
