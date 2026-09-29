using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities.Maintenance;
using FluentValidation;
using MediatR;

namespace Application.Features.Maintenance.Tasks
{
    /// <summary>مهامي، أو مهام قسمي لرئيس القسم، أو الكل للسوبر ادمن — UserId اختياري لفلترة موظف معيّن</summary>
    public record GetMaintenanceTasksQuery(int? UserId, int Page, int PageSize)
        : IRequest<PagedResultDto<MaintenanceTaskResponseDto>>;

    public record GetMaintenanceTaskByIdQuery(int Id) : IRequest<MaintenanceTaskResponseDto>;
    public record CreateMaintenanceTaskCommand(SaveMaintenanceTaskDto Dto) : IRequest<MaintenanceTaskResponseDto>;
    public record UpdateMaintenanceTaskCommand(int Id, SaveMaintenanceTaskDto Dto) : IRequest<MaintenanceTaskResponseDto>;
    public record DeleteMaintenanceTaskCommand(int Id) : IRequest<Unit>;

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

    // ════════════════════ المعالج ════════════════════

    public class MaintenanceTaskHandler :
        IRequestHandler<GetMaintenanceTasksQuery, PagedResultDto<MaintenanceTaskResponseDto>>,
        IRequestHandler<GetMaintenanceTaskByIdQuery, MaintenanceTaskResponseDto>,
        IRequestHandler<CreateMaintenanceTaskCommand, MaintenanceTaskResponseDto>,
        IRequestHandler<UpdateMaintenanceTaskCommand, MaintenanceTaskResponseDto>,
        IRequestHandler<DeleteMaintenanceTaskCommand, Unit>
    {
        private readonly IMaintenanceTaskService _taskService;
        private readonly IUserService _userService;
        private readonly IMapper _mapper;

        public MaintenanceTaskHandler(IMaintenanceTaskService taskService, IUserService userService, IMapper mapper)
        {
            _taskService = taskService;
            _userService = userService;
            _mapper = mapper;
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
                Items = _mapper.Map<List<MaintenanceTaskResponseDto>>(items),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MaintenanceTaskResponseDto> Handle(GetMaintenanceTaskByIdQuery request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);
            var entity = await LoadAsync(request.Id);

            MaintenanceRules.EnsureCanAccess(user, entity.UserId, entity.DepartmentId,
                "لا يمكنك عرض مهمة خارج نطاقك");

            return _mapper.Map<MaintenanceTaskResponseDto>(entity);
        }

        public async Task<MaintenanceTaskResponseDto> Handle(CreateMaintenanceTaskCommand request, CancellationToken ct)
        {
            var user = await MaintenanceRules.CurrentUserAsync(_userService);

            var entity = _mapper.Map<MaintenanceTask>(request.Dto);
            Trim(entity);

            // صاحب المهمة = من سجّلها، والقسم يُحفظ لحظة التسجيل ليراها رئيس القسم
            entity.UserId = user.Id;
            entity.DepartmentId = user.DepartmentId;
            entity.CreatedAt = entity.UpdatedAt = DateTime.UtcNow;

            var created = await _taskService.AddAsync(entity);
            return _mapper.Map<MaintenanceTaskResponseDto>(await LoadAsync(created.Id));
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
            return _mapper.Map<MaintenanceTaskResponseDto>(entity);
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
