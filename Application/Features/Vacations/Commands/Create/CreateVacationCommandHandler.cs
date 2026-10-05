using Application.DTOs.Response;
using Application.Features.Vacations;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Vacations.Commands.Create
{
    /// <summary>
    /// تقديم طلب إجازة (قواعد المستخدم 2026-10-04):
    /// - طلب واحد = سجل واحد. الدفع لا يُحدَّد هنا بل عند الاعتماد النهائي (ApproveVacationCommandHandler).
    /// - المدة تُحسب بأيام العمل: بلا الجمعة ولا العطل الرسمية؛ مدة كلها عطل تُرفض.
    /// - لا تقديم بأثر رجعي، ويُسمح باليوم الحالي.
    /// </summary>
    public class CreateVacationCommandHandler
       : IRequestHandler<CreateVacationCommand, List<VacationResponseDto>>
    {
        private readonly IVacationService _vacationService;
        private readonly IVacationTypeService _vacationTypeService;
        private readonly IPublicHolidayService _holidays;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;

        public CreateVacationCommandHandler(
            IVacationService vacationService,
            IVacationTypeService vacationTypeService,
            IPublicHolidayService holidays,
            IUserService userService,
            IUserPermissionService permissions,
            INotificationService notificationService,
            IMapper mapper)
        {
            _vacationService = vacationService;
            _vacationTypeService = vacationTypeService;
            _holidays = holidays;
            _userService = userService;
            _permissions = permissions;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        public async Task<List<VacationResponseDto>> Handle(
            CreateVacationCommand request, CancellationToken ct)
        {
            var dto = request.VacationDto;
            var start = dto.StartVac.Date;
            var end = dto.EndVac.Date;

            // 1) تحقق من نوع الإجازة
            _ = await _vacationTypeService.GetByIdAsync(dto.VacationTypeId)
                ?? throw new KeyNotFoundException("نوع الإجازة غير موجود");

            // 2) تحقق من التواريخ
            if (end < start)
                throw new ArgumentException("تاريخ النهاية يجب أن يكون بعد تاريخ البداية");
            if (start < DateTime.Today)
                throw new ArgumentException("لا يمكن تقديم إجازة بتاريخ سابق، أقرب تاريخ للبداية هو اليوم");

            // 3) تحقق من المستخدم
            var user = await _userService.GetByIdAsync(request.UserId)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");

            // من لا يتبع لقسم (مدير النظام، أو دور مرتبط بفرع فقط) لا يقدّم إجازات من النظام (قرار المستخدم 2026-09-27)
            if (Application.Common.OrganizationRole.IsSystemAdmin(user))
                throw new InvalidOperationException("لا يمكن لمدير النظام تقديم طلب إجازة من النظام");
            if (user.DepartmentId is not int departmentId || user.BranchId is not int branchId)
                throw new InvalidOperationException("حسابك غير مرتبط بقسم وفرع، فلا يمكنك تقديم إجازة من النظام");

            // 4) أيام العمل في المدة
            var holidays = await _holidays.GetDatesAsync(start, end);
            var workingDays = VacationCalendar.CountWorkingDays(start, end, holidays);
            if (workingDays == 0)
                throw new InvalidOperationException("المدة المختارة كلها أيام عطلة (جمعة أو عطل رسمية)، لا حاجة لطلب إجازة");

            // 5) تحقق من عدم وجود إجازة متداخلة (معلّقة أو معتمدة)
            if (await _vacationService.HasOverlappingVacationAsync(request.UserId, start, end))
                throw new InvalidOperationException("يوجد إجازة متداخلة في نفس الفترة");

            // من يملك صلاحية الموافقة الأولى تتجاوز إجازته تلك المرحلة وتذهب مباشرة للاعتماد النهائي
            var initialStatus = await _permissions.HasAsync(user.Id, Application.Common.AppPermissions.ApproveVacationFirst)
                ? VacationStatus.PendingBranchManager
                : VacationStatus.PendingManager;

            var vacation = new Vacation
            {
                VacationTypeId = dto.VacationTypeId,
                UserId = request.UserId,
                DepartmentId = departmentId,
                BranchId = branchId,
                VacReason = dto.VacReason,
                StartVac = start,
                EndVac = end,
                VacDayCount = workingDays,
                Status = initialStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // المرفقات تُحفظ مع الطلب في نفس العملية (النوع من محتوى الملف، والمدقّق رفض ما سواه)
            foreach (var file in request.Attachments)
                vacation.Attachments.Add(new VacationAttachment
                {
                    FileName = VacationAttachmentRules.SafeFileName(file.FileName),
                    ContentType = VacationAttachmentRules.DetectContentType(file.Data)!,
                    Size = file.Data.Length,
                    UploadedAt = DateTime.UtcNow,
                    Content = new VacationAttachmentContent { Data = file.Data }
                });

            await _vacationService.AddAsync(vacation);

            // إشعار من ينتظر قراره (صاحب الموافقة الأولى، أو الاعتماد النهائي إن تجاوزها)
            await VacationNotifier.NotifySubmittedAsync(
                _permissions, _notificationService, vacation, user.FullName);

            var withDetails = await _vacationService.GetWithDetailsAsync(vacation.Id) ?? vacation;
            return [_mapper.Map<VacationResponseDto>(withDetails)];
        }
    }
}
