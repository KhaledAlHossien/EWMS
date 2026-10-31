using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Contracts.ContractSupport;

namespace Application.Features.Contracts
{
    // العقود: البحث ضمن حدّ المشاريع، التفصيل، الفتح، التعديل، وتغيير الحالة بقرار. العقد لا يُحذف: يُلغى أو يُفسخ.

    public record SearchContractsQuery(int? ProjectId, int? BranchId, int? Status, int? Method, int? ContractorId, string? Search, bool WarrantyEndingOnly, int Page, int PageSize)
        : IRequest<PagedResultDto<ContractListItemDto>>;
    public record GetContractQuery(int Id) : IRequest<ContractDetailDto>;
    public record CreateContractCommand(ContractRequestDto Dto) : IRequest<ContractDetailDto>;
    public record UpdateContractCommand(int Id, ContractRequestDto Dto) : IRequest<ContractDetailDto>;
    public record ChangeContractStatusCommand(int Id, ContractStatusRequestDto Dto) : IRequest<ContractDetailDto>;

    internal static class ContractChecks
    {
        public static IEnumerable<string> Problems(ContractRequestDto dto)
        {
            if (!ContractRules.IsValidMethod(dto.Method)) yield return "طبيعة التعاقد: تنفيذ مباشر أو مناقصة أو عروض أسعار";
            else if (ContractRules.ValidateContract((ContractingMethod)dto.Method, dto.Number, dto.Value, dto.Currency, dto.DurationDays, dto.ContractDate, dto.StartDate,
                dto.Scope, dto.Notes, dto.TenderNumber, dto.TenderSentAt, dto.TenderOpenedAt, dto.AwardedAt, dto.AwardNote,
                dto.WarrantyMonths, dto.WarrantyStart, dto.WarrantyEnd, DateTime.Today) is string problem)
                yield return problem;
            if ((dto.ExecutionOrderNumber ?? string.Empty).Length > 100) yield return "رقم أمر التنفيذ لا يتجاوز 100 حرف";
        }

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class CreateContractCommandValidator : AbstractValidator<CreateContractCommand>
    {
        public CreateContractCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in ContractChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class UpdateContractCommandValidator : AbstractValidator<UpdateContractCommand>
    {
        public UpdateContractCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in ContractChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class ChangeContractStatusCommandValidator : AbstractValidator<ChangeContractStatusCommand>
    {
        public ChangeContractStatusCommandValidator() =>
            RuleFor(x => x.Dto.Status).Must(ContractRules.IsValidStatus).WithMessage("حالة العقد غير معروفة");
    }

    public class SearchContractsHandler(ContractContext ctx) : IRequestHandler<SearchContractsQuery, PagedResultDto<ContractListItemDto>>
    {
        public async Task<PagedResultDto<ContractListItemDto>> Handle(SearchContractsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!AppPermissions.AnyOf["AnyContractView"].Any(viewer.Has) && !viewer.IsSuperAdmin)
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض العقود");
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var today = DateTime.Today;

            ContractStatus? status = request.Status is int s && ContractRules.IsValidStatus(s) ? (ContractStatus)s : null;
            ContractingMethod? method = request.Method is int m && ContractRules.IsValidMethod(m) ? (ContractingMethod)m : null;
            var filter = new ContractFilter(request.ProjectId, request.BranchId, status, method, request.ContractorId, request.Search, request.WarrantyEndingOnly,
                today, ContractContext.WarrantyWindowDays);

            var (items, total) = await ctx.Contracts.SearchAsync(ProjectRules.Visible(viewer), filter, page, pageSize);
            return new PagedResultDto<ContractListItemDto>
            {
                Items = items.Select(c => ToListItem(c, today)).ToList(), TotalCount = total, Page = page, PageSize = pageSize
            };
        }
    }

    public class GetContractHandler(ContractContext ctx) : IRequestHandler<GetContractQuery, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(GetContractQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var contract = await ctx.LoadAsync(viewer, request.Id);
            return ToDetail(contract, viewer, DateTime.Today);
        }
    }

    public class CreateContractHandler(ContractContext ctx) : IRequestHandler<CreateContractCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(CreateContractCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var project = await ctx.Projects.GetByIdAsync(dto.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ContractRules.EnsureCanCreate(viewer, project);

            var method = (ContractingMethod)dto.Method;
            // المتعاقد معه يُدخَل مباشرة في التنفيذ المباشر فقط؛ وللمناقصة وعروض الأسعار يُحدَّد بالإرساء
            if (method != ContractingMethod.Direct && dto.ContractorId != null)
                throw new InvalidOperationException("المتعاقد معه يُحدَّد بالإرساء على عرض عندما لا يكون التعاقد مباشراً");
            var contractor = await ctx.ResolveContractorAsync(dto.ContractorId, project.BranchId);
            var correspondence = await ctx.ResolveCorrespondenceAsync(viewer, dto.CorrespondenceId, project.BranchId);

            var number = ContractChecks.Clean(dto.Number);
            if (number != null && await ctx.Contracts.NumberExistsAsync(project.BranchId, number))
                throw new InvalidOperationException("رقم العقد الرسمي مستعمل في الفرع");

            var contract = await ctx.Contracts.AddAsync(new Contract
            {
                ProjectId = project.Id, BranchId = project.BranchId, CorrespondenceId = correspondence?.Id,
                ExecutionOrderNumber = ContractChecks.Clean(dto.ExecutionOrderNumber), ExecutionOrderDate = dto.ExecutionOrderDate?.Date,
                Method = method, Status = ContractStatus.Contracting, ContractorId = contractor?.Id, Number = number,
                ContractDate = dto.ContractDate?.Date, StartDate = dto.StartDate?.Date, DurationDays = dto.DurationDays, Value = dto.Value, Currency = dto.Currency,
                Scope = (dto.Scope ?? string.Empty).Trim(), Notes = (dto.Notes ?? string.Empty).Trim(),
                TenderNumber = ContractChecks.Clean(dto.TenderNumber), TenderSentAt = dto.TenderSentAt?.Date, TenderOpenedAt = dto.TenderOpenedAt?.Date,
                AwardedAt = dto.AwardedAt?.Date, AwardNote = (dto.AwardNote ?? string.Empty).Trim(),
                WarrantyMonths = dto.WarrantyMonths, WarrantyStart = dto.WarrantyStart?.Date, WarrantyEnd = dto.WarrantyEnd?.Date,
                CreatedByUserId = viewer.Id
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Contract, null,
                $"فتح عقد ({ContractRules.MethodAr(method)}) على المشروع «{project.Name}»",
                Lines(number is null ? null : $"الرقم الرسمي: {number}", contractor is null ? null : $"المتعاقد معه: {contractor.Name}",
                    dto.Value is null ? null : $"القيمة: {Money(dto.Value, dto.Currency)}", correspondence is null ? null : "مرتبط بمعاملة واردة (أمر التنفيذ)")));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    public class UpdateContractHandler(ContractContext ctx) : IRequestHandler<UpdateContractCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(UpdateContractCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.Id);
            ContractRules.EnsureCanEdit(viewer, contract);
            if (dto.ProjectId != contract.ProjectId) throw new InvalidOperationException("لا يُنقل العقد إلى مشروع آخر");

            var method = (ContractingMethod)dto.Method;
            if (method != contract.Method && contract.Offers.Count > 0)
                throw new InvalidOperationException("سُجّلت عروض على العقد: لا تتغير طبيعة التعاقد");
            // لغير المباشر يبقى المتعاقد معه ما حدّده الإرساء
            var contractorId = method == ContractingMethod.Direct ? dto.ContractorId : contract.ContractorId;
            if (method != ContractingMethod.Direct && dto.ContractorId != contract.ContractorId)
                throw new InvalidOperationException("المتعاقد معه يُحدَّد بالإرساء على عرض عندما لا يكون التعاقد مباشراً");
            ContractRules.EnsureCoreFieldsUnchanged(contract, method, contractorId, dto.Value, dto.DurationDays);

            var contractor = await ctx.ResolveContractorAsync(contractorId, contract.BranchId, contract.ContractorId);
            var correspondence = await ctx.ResolveCorrespondenceAsync(viewer, dto.CorrespondenceId, contract.BranchId);
            var number = ContractChecks.Clean(dto.Number);
            if (number != null && await ctx.Contracts.NumberExistsAsync(contract.BranchId, number, contract.Id))
                throw new InvalidOperationException("رقم العقد الرسمي مستعمل في الفرع");

            var details = Lines(
                Change("طبيعة التعاقد", ContractRules.MethodAr(contract.Method), ContractRules.MethodAr(method)),
                Change("الرقم الرسمي", contract.Number, number),
                Change("المتعاقد معه", contract.Contractor?.Name, contractor?.Name),
                Change("تاريخ العقد", contract.ContractDate?.ToString("yyyy-MM-dd"), dto.ContractDate?.ToString("yyyy-MM-dd")),
                Change("بدء التنفيذ", contract.StartDate?.ToString("yyyy-MM-dd"), dto.StartDate?.ToString("yyyy-MM-dd")),
                Change("المدة (يوم)", contract.DurationDays?.ToString(), dto.DurationDays?.ToString()),
                Change("القيمة", Money(contract.Value, contract.Currency), Money(dto.Value, dto.Currency)),
                Change("النطاق", contract.Scope, dto.Scope),
                Change("رقم المناقصة", contract.TenderNumber, dto.TenderNumber),
                Change("إرسال المناقصة", contract.TenderSentAt?.ToString("yyyy-MM-dd"), dto.TenderSentAt?.ToString("yyyy-MM-dd")),
                Change("فض المناقصة", contract.TenderOpenedAt?.ToString("yyyy-MM-dd"), dto.TenderOpenedAt?.ToString("yyyy-MM-dd")),
                Change("الإرساء", contract.AwardedAt?.ToString("yyyy-MM-dd"), dto.AwardedAt?.ToString("yyyy-MM-dd")),
                Change("مدة الضمان (شهر)", contract.WarrantyMonths?.ToString(), dto.WarrantyMonths?.ToString()),
                Change("بداية الضمان", contract.WarrantyStart?.ToString("yyyy-MM-dd"), dto.WarrantyStart?.ToString("yyyy-MM-dd")),
                Change("نهاية الضمان", contract.WarrantyEnd?.ToString("yyyy-MM-dd"), dto.WarrantyEnd?.ToString("yyyy-MM-dd")),
                Change("أمر التنفيذ", contract.ExecutionOrderNumber, dto.ExecutionOrderNumber),
                Change("الملاحظات", contract.Notes, dto.Notes));

            contract.CorrespondenceId = correspondence?.Id;
            contract.ExecutionOrderNumber = ContractChecks.Clean(dto.ExecutionOrderNumber);
            contract.ExecutionOrderDate = dto.ExecutionOrderDate?.Date;
            contract.Method = method;
            contract.ContractorId = contractor?.Id;
            contract.Number = number;
            contract.ContractDate = dto.ContractDate?.Date;
            contract.StartDate = dto.StartDate?.Date;
            contract.DurationDays = dto.DurationDays;
            contract.Value = dto.Value;
            contract.Currency = dto.Currency;
            contract.Scope = (dto.Scope ?? string.Empty).Trim();
            contract.Notes = (dto.Notes ?? string.Empty).Trim();
            contract.TenderNumber = ContractChecks.Clean(dto.TenderNumber);
            contract.TenderSentAt = dto.TenderSentAt?.Date;
            contract.TenderOpenedAt = dto.TenderOpenedAt?.Date;
            contract.AwardedAt = dto.AwardedAt?.Date;
            contract.AwardNote = (dto.AwardNote ?? string.Empty).Trim();
            contract.WarrantyMonths = dto.WarrantyMonths;
            contract.WarrantyStart = dto.WarrantyStart?.Date;
            contract.WarrantyEnd = dto.WarrantyEnd?.Date;

            await ctx.Contracts.UpdateAsync(contract, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Contract, contract.Id, $"تعديل العقد {ContractContext.Label(contract)}", details));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>تغيير الحالة بقرار يدوي: ساري ← منجز أو مفسوخ، وقيد التعاقد ← ساري أو ملغى؛ السريان يشترط اكتمال بيانات التعاقد</summary>
    public class ChangeContractStatusHandler(ContractContext ctx) : IRequestHandler<ChangeContractStatusCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(ChangeContractStatusCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.Id);
            var to = (ContractStatus)dto.Status;
            ContractRules.EnsureCanChangeStatus(viewer, contract, to, dto.Reason);

            var from = contract.Status;
            contract.Status = to;
            var action = to switch { ContractStatus.Cancelled or ContractStatus.Terminated => AuditActions.Cancelled, _ => AuditActions.Stage };
            await ctx.Contracts.UpdateAsync(contract, dto.RowVersion,
                new AuditEntry(viewer.Id, action, AuditEntities.Contract, contract.Id,
                    $"العقد {ContractContext.Label(contract)}: {ContractRules.StatusAr(from)} ← {ContractRules.StatusAr(to)}",
                    string.IsNullOrWhiteSpace(dto.Reason) ? string.Empty : $"السبب: {dto.Reason.Trim()}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }
}
