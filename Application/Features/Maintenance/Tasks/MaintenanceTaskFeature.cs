using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.Tasks
{
    /// <summary>مهامي، أو مهام قسمي لرئيس القسم، أو فرعي لرئيس الفرع (اطلاع)، أو الكل للسوبر ادمن — UserId اختياري لفلترة موظف معيّن</summary>
    public record GetMaintenanceTasksQuery(int? UserId, int Page, int PageSize)
        : IRequest<PagedResultDto<MaintenanceTaskResponseDto>>;

    public record GetMaintenanceTaskByIdQuery(int Id) : IRequest<MaintenanceTaskResponseDto>;
    public record CreateMaintenanceTaskCommand(SaveMaintenanceTaskDto Dto) : IRequest<MaintenanceTaskResponseDto>;
    public record UpdateMaintenanceTaskCommand(int Id, SaveMaintenanceTaskDto Dto) : IRequest<MaintenanceTaskResponseDto>;
    public record DeleteMaintenanceTaskCommand(int Id) : IRequest<Unit>;

    /// <summary>نقل المهمة إلى موظف آخر — رئيس القسم (داخل قسمه) أو السوبر ادمن</summary>
    public record AssignMaintenanceTaskCommand(int Id, int UserId) : IRequest<MaintenanceTaskResponseDto>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class SaveMaintenanceTaskDtoValidator : AbstractValidator<SaveMaintenanceTaskDto>
    {
        public SaveMaintenanceTaskDtoValidator()
        {
            RuleFor(x => x.TaskLocation)
                .NotEmpty().WithMessage("مكان المهمة مطلوب")
                .MaximumLength(200).WithMessage("مكان المهمة لا يتجاوز 200 حرف");

            RuleFor(x => x.RequestingParty)
                .NotEmpty().WithMessage("الجهة الطالبة مطلوبة")
                .MaximumLength(200).WithMessage("الجهة الطالبة لا تتجاوز 200 حرف");

            RuleFor(x => x.RequiredWork)
                .NotEmpty().WithMessage("الأعمال المطلوبة مطلوبة")
                .MaximumLength(2000).WithMessage("الأعمال المطلوبة لا تتجاوز 2000 حرف");

            RuleFor(x => x.CompletedWorks)
                .MaximumLength(2000).WithMessage("الأعمال المنجزة لا تتجاوز 2000 حرف");

            RuleFor(x => x.CompletedAt)
                .GreaterThanOrEqualTo(x => x.StartedAt!.Value)
                .When(x => x.StartedAt != null && x.CompletedAt != null)
                .WithMessage("تاريخ الإنجاز لا يمكن أن يسبق تاريخ البدء");
        }
    }

    public class CreateMaintenanceTaskCommandValidator : AbstractValidator<CreateMaintenanceTaskCommand>
    {
        public CreateMaintenanceTaskCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceTaskDtoValidator());
    }

    public class UpdateMaintenanceTaskCommandValidator : AbstractValidator<UpdateMaintenanceTaskCommand>
    {
        public UpdateMaintenanceTaskCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new SaveMaintenanceTaskDtoValidator());
    }

    public class AssignMaintenanceTaskCommandValidator : AbstractValidator<AssignMaintenanceTaskCommand>
    {
        public AssignMaintenanceTaskCommandValidator() =>
            RuleFor(x => x.UserId).GreaterThan(0).WithMessage("يجب اختيار الموظف");
    }

    // ════════════════════ المعالج ════════════════════

    public class MaintenanceTaskHandler :
        IRequestHandler<GetMaintenanceTasksQuery, PagedResultDto<MaintenanceTaskResponseDto>>,
        IRequestHandler<GetMaintenanceTaskByIdQuery, MaintenanceTaskResponseDto>,
        IRequestHandler<CreateMaintenanceTaskCommand, MaintenanceTaskResponseDto>,
        IRequestHandler<UpdateMaintenanceTaskCommand, MaintenanceTaskResponseDto>,
        IRequestHandler<DeleteMaintenanceTaskCommand, Unit>,
        IRequestHandler<AssignMaintenanceTaskCommand, MaintenanceTaskResponseDto>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly INotificationService _notifications;
        private readonly IMapper _mapper;

        public MaintenanceTaskHandler(
            IMaintenanceTaskService taskService,
            IUserService userService,
            INotificationService notifications,
            IMapper mapper)
        {
            _taskService = taskService;
            _userService = userService;
            _notifications = notifications;
            _mapper = mapper;
        }

        // التحويل إلى DTO مع ما يستطيعه المستخدم الحالي على المهمة
        private MaintenanceTaskResponseDto ToDto(MaintenanceTask entity, User viewer)
        {
            var dto = _mapper.Map<MaintenanceTaskResponseDto>(entity);
            dto.CanEdit = MaintenanceRules.CanAccess(viewer, entity.UserId, entity.DepartmentId);
            dto.CanAssign = MaintenanceRules.CanAssign(viewer, entity.DepartmentId);
            return dto;
        }

        private async Task<MaintenanceTask> LoadAsync(int id) =>
            await _taskService.GetByIdAsync(id) ?? throw new KeyNotFoundException("المهمة غير موجودة");

        private static void Trim(MaintenanceTask t)
        {
            t.TaskLocation = t.TaskLocation.Trim();
            t.RequestingParty = t.RequestingParty.Trim();
            t.RequiredWork = t.RequiredWork.Trim();
            t.CompletedWorks = t.CompletedWorks.Trim();
        }

        public async Task<PagedResultDto<MaintenanceTaskResponseDto>> Handle(GetMaintenanceTasksQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var (page, pageSize) = MaintenanceRules.NormalizePaging(request.Page, request.PageSize);

            var (items, total) = await _taskService.GetPageAsync(
                MaintenanceRules.TaskScope(user), request.UserId, page, pageSize);

            return new PagedResultDto<MaintenanceTaskResponseDto>
            {
                Items = items.Select(t => ToDto(t, user)).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MaintenanceTaskResponseDto> Handle(GetMaintenanceTaskByIdQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanView(user, entity.UserId, entity.Department,
                "لا يمكنك عرض مهمة خارج نطاقك");

            return ToDto(entity, user);
        }

        public async Task<MaintenanceTaskResponseDto> Handle(CreateMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);

            var entity = _mapper.Map<MaintenanceTask>(request.Dto);
            Trim(entity);

            // صاحب المهمة = الموظف الذي وجّهها إليه رئيس القسم، وإلا من سجّلها.
            // القسم يُحفظ لحظة التسجيل ليراها رئيس القسم
            var owner = request.Dto.AssigneeId is int assigneeId && assigneeId != user.Id
                ? await MaintenanceRules.ResolveAssigneeAsync(_userService, user, user.DepartmentId, assigneeId)
                : user;

            entity.UserId = owner.Id;
            entity.DepartmentId = owner.DepartmentId;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            var created = await _taskService.AddAsync(entity);
            var loaded = await LoadAsync(created.Id);

            if (owner.Id != user.Id)
                await MaintenanceNotifier.TaskAssignedAsync(_notifications, loaded, user);

            return ToDto(loaded, user);
        }

        public async Task<MaintenanceTaskResponseDto> Handle(UpdateMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك تعديل مهمة خارج نطاقك");

            _mapper.Map(request.Dto, entity);
            Trim(entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _taskService.UpdateAsync(entity);
            return ToDto(entity, user);
        }

        public async Task<MaintenanceTaskResponseDto> Handle(AssignMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            var target = await MaintenanceRules.ResolveAssigneeAsync(_userService, user, entity.DepartmentId, request.UserId);

            if (target.Id == entity.UserId)
                return ToDto(entity, user);

            var previousUserId = entity.UserId;

            entity.UserId = target.Id;
            entity.DepartmentId = target.DepartmentId;
            entity.UpdatedAt = DateTime.UtcNow;
            await _taskService.UpdateAsync(entity);

            var updated = await LoadAsync(entity.Id);
            await MaintenanceNotifier.TaskReassignedAsync(_notifications, updated, user, previousUserId, target);

            return ToDto(updated, user);
        }

        public async Task<Unit> Handle(DeleteMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك حذف مهمة خارج نطاقك");

            await _taskService.DeleteAsync(entity);
            return Unit.Value;
        }
    }
}
