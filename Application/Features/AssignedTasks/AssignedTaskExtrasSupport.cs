using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Features.AssignedTasks
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class AssignedTaskExtrasSupport
    {
        private readonly IAssignedTaskService _tasks;

        public AssignedTaskExtrasSupport(IAssignedTaskService tasks)
        {
            _tasks = tasks;
        }

        public async Task<AssignedTask> LoadAsync(int id) =>
            await _tasks.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");

        public async Task<AssignedTaskDetailDto> DetailAsync(int id, Viewer viewer) =>
            AssignedTaskRules.ToDetail(await LoadAsync(id), viewer);

        public static void EnsureOpen(AssignedTask t, Viewer viewer)
        {
            if (t.Status == AssignedTaskStatus.Done)
                throw new InvalidOperationException("المهمة منجزة — سجلها ومرفقاتها وقائمتها ثابتة لا تتغيّر");
            AssignedTaskRules.EnsureNotFrozen(t, viewer);
        }

        public static void EnsureCanManageChecklist(AssignedTask task, Viewer viewer)
        {
            if (!(task.CreatedByUserId == viewer.Id || AssignedTaskRules.CanHandle(task, viewer) || viewer.IsSuperAdmin))
                throw new UnauthorizedAccessException("قائمة التحقق للمُسنِد والجهة المنفِّذة فقط");
            EnsureOpen(task, viewer);
        }
    }
}
