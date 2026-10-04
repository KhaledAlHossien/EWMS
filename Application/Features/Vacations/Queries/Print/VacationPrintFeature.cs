
        // الرقم مخزّن كعدد فيضيع الصفر الأول: 9 خانات = 09XXXXXXXX بعد إعادة الصفر
        private static string FormatPhone(int phone)
        {
            var text = phone.ToString();
            return text.Length == 9 ? "0" + text : text;
        }
using System.Globalization;
using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.Features.Vacations.Queries.Print
{
    public record GetVacationPrintQuery(int Id) : IRequest<VacationPrintDto>;

    /// <summary>
    /// نموذج "طلب إجازة" للطباعة: يحتاج PrintVacation، وحدّه = ما يستطيع المستخدم عرضه (VacationAccess.CanView).
    /// "رأي رئيس الفرع" = قرار المرحلة النهائية ومن أبداه (المعتمِد أو الرافض)، مع توقيعه إن رفعه من ملفه الشخصي.
    /// </summary>
    public class GetVacationPrintQueryHandler : IRequestHandler<GetVacationPrintQuery, VacationPrintDto>
    {
        private readonly IVacationService _service;
        private readonly IVacationTypeService _types;
        private readonly IUserService _userService;
        private readonly IUserPermissionService _permissions;
        private readonly IUserSignatureService _signatures;
        private readonly IMapper _mapper;

        public GetVacationPrintQueryHandler(
            IVacationService service,
            IVacationTypeService types,
            IUserService userService,
            IUserPermissionService permissions,
            IUserSignatureService signatures,
            IMapper mapper)
        {
            _service = service;
            _types = types;
            _userService = userService;
            _permissions = permissions;
            _signatures = signatures;
            _mapper = mapper;
        }

        public async Task<VacationPrintDto> Handle(GetVacationPrintQuery request, CancellationToken ct)
        {
            var vacation = await _service.GetWithDetailsAsync(request.Id)
                ?? throw new KeyNotFoundException("الإجازة غير موجودة");

            var viewer = await Viewer.CurrentAsync(_userService, _permissions);
            if (!VacationAccess.CanView(viewer, vacation))
                throw new UnauthorizedAccessException("لا تملك صلاحية طباعة هذه الإجازة");

            // CreatedAt محفوظ بتوقيت UTC، والورقة تُطبع بتاريخ الخادم المحلي
            var submitted = DateTime.SpecifyKind(vacation.CreatedAt, DateTimeKind.Utc).ToLocalTime();
            var user = vacation.User;

            var result = new VacationPrintDto
            {
                Vacation = _mapper.Map<VacationResponseDto>(vacation),
                SubmittedAt = submitted,
                SubmittedHijri = HijriDate(submitted),
                EmployeeName = user?.FullName ?? string.Empty,
                PersonalIdNumber = user is { PersonalIdNumber: > 0 } ? user.PersonalIdNumber.ToString() : null,
                Phone = user is { PhoneNumber: > 0 } ? FormatPhone(user.PhoneNumber) : null,
                DepartmentName = vacation.Department?.Name ?? string.Empty,
                BranchName = vacation.Branch?.Name ?? string.Empty,
                Types = (await _types.GetAllAsync())
                    .OrderBy(t => t.Id)
                    .Select(t => new VacationPrintTypeDto { Id = t.Id, Name = t.Name, Selected = t.Id == vacation.VacationTypeId })
                    .ToList()
            };

            var (opinion, signer) = BranchDecision(vacation);
            result.BranchOpinion = opinion;
            if (signer != null)
            {
                result.SignerName = signer.FullName;
                result.SignerSignature = await _signatures.GetAsync(signer.Id);
            }

            return result;
        }

        /// <summary>رأي المرحلة النهائية ومن أبداه. رفض المرحلة الأولى أو الإلغاء يُكتب بلا توقيع رئيس الفرع.</summary>
        private static (string? Opinion, User? Signer) BranchDecision(Vacation v) => v.Status switch
        {
            VacationStatus.Approved =>
                ($"موافق ({VacationRules.PaymentAr(v)})", v.FinalApprovedByUser),
            VacationStatus.Rejected when v.RejectedAtStage == VacationStatus.PendingBranchManager =>
                ($"غير موافق. السبب: {v.RejectionReason}", v.RejectedByUser),
            VacationStatus.Rejected =>
                ($"رُفض الطلب في مرحلة الموافقة الأولى. السبب: {v.RejectionReason}", null),
            VacationStatus.Cancelled => ("ألغى الموظف الطلب قبل البت فيه", null),
            _ => (null, null)
        };

        private static string HijriDate(DateTime date)
        {
            var calendar = new UmAlQuraCalendar();
            date = date.Date.AddDays(VacationRules.HijriDayOffset);
            return $"{calendar.GetYear(date)}/{calendar.GetMonth(date)}/{calendar.GetDayOfMonth(date)}";
        }
    }
}
