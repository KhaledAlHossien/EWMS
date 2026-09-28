using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using MediatR;

namespace Application.Features.WorkTasks.Commands.Create
{
    public class CreateWorkTaskCommandHandler : IRequestHandler<CreateWorkTaskCommand, WorkTaskResponseDto>
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IBranchService _branchService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public CreateWorkTaskCommandHandler(
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

        public async Task<WorkTaskResponseDto> Handle(CreateWorkTaskCommand request, CancellationToken cancellationToken)
        {
            var dto = request.TaskDto;
            var name = dto.Name.Trim();

            if (!await _branchService.ExistsAsync(dto.BranchId))
                throw new KeyNotFoundException("الفرع غير موجود");

            if (await _workTaskService.ExistsByNameAsync(dto.BranchId, name))
                throw new InvalidOperationException("توجد مهمة بنفس الاسم في هذا الفرع");

            var userIds = dto.UserIds.Distinct().ToList();
            await WorkTaskRules.EnsureAssigneesInBranchAsync(_userService, dto.BranchId, userIds);

            var now = DateTime.UtcNow;
            var task = new WorkTask
            {
                Name = name,
                Description = dto.Description.Trim(),
                Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "📋" : dto.Icon.Trim(),
                BranchId = dto.BranchId,
                IsActive = dto.IsActive,
                CreatedAt = now,
                Assignments = userIds.Select(id => new UserWorkTask { UserId = id, AssignedAt = now }).ToList()
            };

            await _workTaskService.AddAsync(task);

            if (task.IsActive)
                await WorkTaskRules.NotifyNewAssigneesAsync(_notificationService, task, userIds);

            var created = await _workTaskService.GetByIdAsync(task.Id) ?? task;
            return _mapper.Map<WorkTaskResponseDto>(created);
        }
    }
}
