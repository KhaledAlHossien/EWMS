using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.Attendance
{
    public record GetAttendanceAreasQuery : IRequest<List<AttendanceAreaDto>>;
    public record GetAttendanceAreaQuery(int Id) : IRequest<AttendanceAreaDto>;
    public record GetAreaBranchOptionsQuery : IRequest<List<AreaBranchOptionDto>>;
    public record CreateAttendanceAreaCommand(AttendanceAreaRequestDto Dto) : IRequest<AttendanceAreaDto>;
    public record UpdateAttendanceAreaCommand(int Id, AttendanceAreaRequestDto Dto) : IRequest<AttendanceAreaDto>;
    public record DeleteAttendanceAreaCommand(int Id) : IRequest<Unit>;

    public class AttendanceAreaRequestValidator : AbstractValidator<AttendanceAreaRequestDto>
    {
        public AttendanceAreaRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المنطقة مطلوب").MaximumLength(100).WithMessage("اسم المنطقة طويل جداً");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف طويل جداً");
            RuleFor(x => x.BranchIds).Must(ids => ids.Count <= 500).WithMessage("عدد الفروع كبير جداً");
        }
    }

    public class CreateAttendanceAreaCommandValidator : AbstractValidator<CreateAttendanceAreaCommand>
    {
        public CreateAttendanceAreaCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceAreaRequestValidator());
    }

    public class UpdateAttendanceAreaCommandValidator : AbstractValidator<UpdateAttendanceAreaCommand>
    {
        public UpdateAttendanceAreaCommandValidator() => RuleFor(x => x.Dto).SetValidator(new AttendanceAreaRequestValidator());
    }

    /// <summary>
    /// مناطق الدوام: سجل واحد لكل المؤسسة يُدار من المركز (ViewAttendanceAreas / Create / Edit / Delete — الحد: كل المناطق).
    /// تغيير فروع منطقة يعيد مزامنة أجهزتها وأجهزة المنطقة التي خرج منها الفرع.
    /// </summary>
    public class AttendanceAreaHandler :
        IRequestHandler<GetAttendanceAreasQuery, List<AttendanceAreaDto>>,
        IRequestHandler<GetAttendanceAreaQuery, AttendanceAreaDto>,
        IRequestHandler<GetAreaBranchOptionsQuery, List<AreaBranchOptionDto>>,
        IRequestHandler<CreateAttendanceAreaCommand, AttendanceAreaDto>,
        IRequestHandler<UpdateAttendanceAreaCommand, AttendanceAreaDto>,
        IRequestHandler<DeleteAttendanceAreaCommand, Unit>
    {
        private readonly IAttendanceAreaService _areas;
        private readonly IAttendanceSettings _settings;

        public AttendanceAreaHandler(IAttendanceAreaService areas, IAttendanceSettings settings)
        {
            _areas = areas;
            _settings = settings;
        }

        private DateTime OnlineSince => DateTime.UtcNow.AddMinutes(-_settings.OfflineMinutes);

        private static AttendanceAreaDto ToDto(AreaSummary s) => new()
        {
            Id = s.Area.Id,
            Name = s.Area.Name,
            Description = s.Area.Description,
            Branches = s.Branches.OrderBy(b => b.Name).Select(b => new IdNameDto { Id = b.Id, Name = b.Name }).ToList(),
            Devices = s.Devices,
            OnlineDevices = s.OnlineDevices,
            People = s.People,
            RowVersion = s.Area.RowVersion
        };

        private async Task<AttendanceAreaDto> DtoAsync(int id) =>
            ToDto(await _areas.GetSummaryAsync(id, OnlineSince) ?? throw new KeyNotFoundException("المنطقة غير موجودة"));

        public async Task<List<AttendanceAreaDto>> Handle(GetAttendanceAreasQuery request, CancellationToken ct) =>
            (await _areas.GetAllAsync(OnlineSince)).Select(ToDto).ToList();

        public Task<AttendanceAreaDto> Handle(GetAttendanceAreaQuery request, CancellationToken ct) => DtoAsync(request.Id);

        public async Task<List<AreaBranchOptionDto>> Handle(GetAreaBranchOptionsQuery request, CancellationToken ct) =>
            (await _areas.GetBranchesAsync()).OrderBy(b => b.Name).Select(b => new AreaBranchOptionDto
            {
                Id = b.Id,
                Name = b.Name,
                AreaId = b.AttendanceAreaId,
                AreaName = b.AttendanceArea?.Name ?? string.Empty
            }).ToList();

        private async Task<List<int>> BranchIdsAsync(IEnumerable<int> requested)
        {
            var ids = requested.Distinct().ToList();
            var known = (await _areas.GetBranchesAsync()).Select(b => b.Id).ToHashSet();
            if (ids.Any(id => !known.Contains(id))) throw new KeyNotFoundException("أحد الفروع المختارة غير موجود");
            return ids;
        }

        public async Task<AttendanceAreaDto> Handle(CreateAttendanceAreaCommand request, CancellationToken ct)
        {
            var name = request.Dto.Name.Trim();
            if (await _areas.ExistsByNameAsync(name))
                throw new InvalidOperationException("توجد منطقة بنفس الاسم");

            var area = new Domain.Entities.Attendance.AttendanceArea
            {
                Name = name,
                Description = request.Dto.Description?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            await _areas.SaveAsync(area, null, await BranchIdsAsync(request.Dto.BranchIds));
            return await DtoAsync(area.Id);
        }

        public async Task<AttendanceAreaDto> Handle(UpdateAttendanceAreaCommand request, CancellationToken ct)
        {
            var area = await _areas.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("المنطقة غير موجودة");
            var name = request.Dto.Name.Trim();
            if (await _areas.ExistsByNameAsync(name, request.Id))
                throw new InvalidOperationException("توجد منطقة أخرى بنفس الاسم");

            area.Name = name;
            area.Description = request.Dto.Description?.Trim() ?? string.Empty;
            await _areas.SaveAsync(area, request.Dto.RowVersion, await BranchIdsAsync(request.Dto.BranchIds));
            return await DtoAsync(area.Id);
        }

        public async Task<Unit> Handle(DeleteAttendanceAreaCommand request, CancellationToken ct)
        {
            var area = await _areas.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("المنطقة غير موجودة");
            if (await _areas.DeleteBlockerAsync(area.Id) is string blocker)
                throw new InvalidOperationException(blocker);
            await _areas.DeleteAsync(area);
            return Unit.Value;
        }
    }
}
