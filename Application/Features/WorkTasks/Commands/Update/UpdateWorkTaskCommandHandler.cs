using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.WorkTasks.Commands.Update
{
    public class UpdateWorkTaskCommandHandler : IRequestHandler<UpdateWorkTaskCommand, WorkTaskResponseDto>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IBranchService _branchService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public UpdateWorkTaskCommandHandler(
            IWorkTaskService workTaskService,
            IBranchService branchService,
            IUserService userService,
            INotificationService notificationService,
            IMapper mapper)
        {
            _workTaskService = workTaskService;
            _branchService = branchService;
            _userService = userService;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<WorkTaskResponseDto> Handle(UpdateWorkTaskCommand request, CancellationToken cancellationToken)
        {
            var dto = request.TaskDto;
            var name = dto.Name.Trim();

            var task = await _workTaskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            if (!await _branchService.ExistsAsync(dto.BranchId))
                throw new KeyNotFoundException("الفرع غير موجود");

            if (await _workTaskService.ExistsByNameAsync(dto.BranchId, name, task.Id))
                throw new InvalidOperationException("توجد مهمة بنفس الاسم في هذا الفرع");

            // عند نقل المهمة لفرع آخر يجب أن يكون كل المسنَدين من الفرع الجديد
            var userIds = dto.UserIds.Distinct().ToList();
            await WorkTaskRules.EnsureAssigneesInBranchAsync(_userService, dto.BranchId, userIds);

            // من سيُبلَّغ: المسنَدون الجدد، أو الجميع إن كانت المهمة معطّلة وأُعيد تفعيلها
            var previousUserIds = task.IsActive
                ? task.Assignments.Select(a => a.UserId).ToHashSet()
                : new HashSet<int>();

            task.Name = name;
            task.Description = dto.Description.Trim();
            task.Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "📋" : dto.Icon.Trim();
            task.BranchId = dto.BranchId;
            task.IsActive = dto.IsActive;

            // مزامنة الإسنادات: حذف من أُزيل، وإضافة الجدد (مع الإبقاء على تاريخ الإسناد القديم)
            var wanted = userIds.ToHashSet();
            foreach (var removed in task.Assignments.Where(a => !wanted.Contains(a.UserId)).ToList())
                task.Assignments.Remove(removed);

            var existing = task.Assignments.Select(a => a.UserId).ToHashSet();
            var now = DateTime.UtcNow;
            foreach (var userId in userIds.Where(id => !existing.Contains(id)))
                task.Assignments.Add(new UserWorkTask { UserId = userId, WorkTaskId = task.Id, AssignedAt = now });

            await _workTaskService.UpdateAsync(task);

            if (task.IsActive)
                await WorkTaskRules.NotifyNewAssigneesAsync(
                    _notificationService, task, userIds.Where(id => !previousUserIds.Contains(id)));

            var updated = await _workTaskService.GetByIdAsync(task.Id) ?? task;
            return _mapper.Map<WorkTaskResponseDto>(updated);
        }
    }
}
