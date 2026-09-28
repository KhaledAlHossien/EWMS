using System.Linq.Expressions;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.AssignedTasks
{
    /// <summary>
    /// Mode: incoming (الواردة — أنا المنفِّذ)، outgoing (الصادرة — أنا المُسنِد)، scope (كل مهام نطاقي).
    /// المنجزة تُعرض لآخر 30 يوماً فقط حتى لا تزدحم اللوحة.
    /// </summary>
    public record GetTaskBoardQuery(string Mode) : IRequest<TaskBoardDto>;
    public record GetAssignedTaskQuery(int Id) : IRequest<AssignedTaskDetailDto>;
    /// <summary>الجهات التي يستطيع المستخدم الإسناد إليها (أقسام فرعه / مكاتب قسمه / موظفو مكتبه)</summary>
    public record GetTaskTargetsQuery : IRequest<List<TaskTargetOptionDto>>;

    public class AssignedTaskQueriesHandler :
        IRequestHandler<GetTaskBoardQuery, TaskBoardDto>,
        IRequestHandler<GetAssignedTaskQuery, AssignedTaskDetailDto>,
        IRequestHandler<GetTaskTargetsQuery, List<TaskTargetOptionDto>>
    {
        private readonly IAssignedTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly IOfficeService _officeService;

        public AssignedTaskQueriesHandler(
            IAssignedTaskService taskService,
            IUserService userService,
            IDepartmentService departmentService,
            IOfficeService officeService)
        {
            _taskService = taskService;
            _userService = userService;
            _departmentService = departmentService;
            _officeService = officeService;
        }

        private async Task<User> CurrentAsync() =>
            await _userService.GetByIdAsync(_userService.UserId)
            ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");

        public async Task<TaskBoardDto> Handle(GetTaskBoardQuery request, CancellationToken ct)
        {
            var user = await CurrentAsync();
            var mode = (request.Mode ?? "incoming").ToLowerInvariant();
            var userId = user.Id;

            Expression<Func<AssignedTask, bool>> filter = mode switch
            {
                "outgoing" => t => t.CreatedByUserId == userId,
                "scope" => AssignedTaskRules.Scope(user),
                _ => AssignedTaskRules.Incoming(user)
            };

            var doneSince = DateTime.UtcNow.AddDays(-30);
            var tasks = await _taskService.GetBoardAsync(filter);
            var targetType = AssignedTaskRules.TargetTypeFor(user.Role?.Name ?? "");

            return new TaskBoardDto
            {
                Mode = mode is "outgoing" or "scope" ? mode : "incoming",
                CanCreate = targetType != null,
                TargetTypeLabel = AssignedTaskRules.TargetTypeLabel(targetType),
                Tasks = tasks
                    .Where(t => t.Status != AssignedTaskStatus.Done || (t.CompletedAt ?? t.UpdatedAt) >= doneSince)
                    .Select(t => AssignedTaskRules.ToCard(t, user))
                    .ToList()
            };
        }

        public async Task<AssignedTaskDetailDto> Handle(GetAssignedTaskQuery request, CancellationToken ct)
        {
            var user = await CurrentAsync();
            var task = await _taskService.GetByIdAsync(request.Id)
                ?? throw new KeyNotFoundException("المهمة غير موجودة");

            if (!AssignedTaskRules.CanView(task, user))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");

            return AssignedTaskRules.ToDetail(task, user);
        }

        public async Task<List<TaskTargetOptionDto>> Handle(GetTaskTargetsQuery request, CancellationToken ct)
        {
            var user = await CurrentAsync();

            switch (AssignedTaskRules.TargetTypeFor(user.Role?.Name ?? ""))
            {
                case AssignedTaskTargetType.Department when user.BranchId is int branchId:
                {
                    var branchUsers = await _userService.GetByBranchAsync(branchId);
                    return (await _departmentService.GetAllAsync())
                        .Where(d => d.BranchId == branchId).OrderBy(d => d.Name)
                        .Select(d => new TaskTargetOptionDto
                        {
                            Id = d.Id,
                            Name = d.Name,
                            HeadNames = string.Join("، ", branchUsers
                                .Where(u => u.IsActive && u.DepartmentId == d.Id && u.Role?.Name == "Manager")
                                .Select(u => u.FullName))
                        }).ToList();
                }
                case AssignedTaskTargetType.Office when user.DepartmentId is int departmentId:
                {
                    var deptUsers = await _userService.GetByDepartmentAsync(departmentId);
                    return (await _officeService.GetByDepartmentAsync(departmentId))
                        .OrderBy(o => o.Name)
                        .Select(o => new TaskTargetOptionDto
                        {
                            Id = o.Id,
                            Name = o.Name,
                            HeadNames = string.Join("، ", deptUsers
                                .Where(u => u.IsActive && u.OfficeId == o.Id && u.Role?.Name == "OfficeManager")
                                .Select(u => u.FullName))
                        }).ToList();
                }
                case AssignedTaskTargetType.User when user.OfficeId is int officeId:
                    return (await _userService.GetByOfficeAsync(officeId))
                        .Where(u => u.IsActive && u.Id != user.Id).OrderBy(u => u.FullName)
                        .Select(u => new TaskTargetOptionDto { Id = u.Id, Name = u.FullName })
                        .ToList();
                default:
                    return [];
            }
        }
    }
}
