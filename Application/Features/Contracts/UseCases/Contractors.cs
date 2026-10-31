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
    // المقاولون ومعايير التقييم (بيانات الفرع) وملف المقاول: قراءتها لمن يتعاقد أو يقيّم أو يفلتر، وإدارتها بصلاحياتها الأربع لكل منهما.

    public record GetContractorsQuery(int? BranchId, bool IncludeInactive) : IRequest<List<ContractorResponseDto>>;
    public record CreateContractorCommand(ContractorRequestDto Dto) : IRequest<ContractorResponseDto>;
    public record UpdateContractorCommand(int Id, ContractorRequestDto Dto) : IRequest<ContractorResponseDto>;
    public record DeleteContractorCommand(int Id) : IRequest<bool>;
    /// <summary>ملف المقاول: تقييماته المراجَعة على عقود مشاريع يراها المستخدم</summary>
    public record GetContractorEvaluationsQuery(int ContractorId) : IRequest<List<ContractorEvaluationHistoryDto>>;

    public record GetEvaluationCriteriaQuery(int? BranchId, bool IncludeInactive) : IRequest<List<EvaluationCriterionResponseDto>>;
    public record CreateEvaluationCriterionCommand(EvaluationCriterionRequestDto Dto) : IRequest<EvaluationCriterionResponseDto>;
    public record UpdateEvaluationCriterionCommand(int Id, EvaluationCriterionRequestDto Dto) : IRequest<EvaluationCriterionResponseDto>;
    public record DeleteEvaluationCriterionCommand(int Id) : IRequest<bool>;

    internal static class ContractorChecks
    {
        public static IEnumerable<string> Problems(ContractorRequestDto dto)
        {
            if (ContractRules.ValidateContractor(dto.Name, dto.RegistrationNumber, dto.ContactName, dto.Phone, dto.Address, dto.Notes) is string problem) yield return problem;
        }

        public static IEnumerable<string> Problems(EvaluationCriterionRequestDto dto)
        {
            if (ContractRules.ValidateCriterion(dto.Name, dto.Description, dto.Weight, dto.SortOrder) is string problem) yield return problem;
        }

        public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class CreateContractorCommandValidator : AbstractValidator<CreateContractorCommand>
    {
        public CreateContractorCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in ContractorChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class UpdateContractorCommandValidator : AbstractValidator<UpdateContractorCommand>
    {
        public UpdateContractorCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in ContractorChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class CreateEvaluationCriterionCommandValidator : AbstractValidator<CreateEvaluationCriterionCommand>
    {
        public CreateEvaluationCriterionCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in ContractorChecks.Problems(dto)) c.AddFailure(problem); });
    }

    public class UpdateEvaluationCriterionCommandValidator : AbstractValidator<UpdateEvaluationCriterionCommand>
    {
        public UpdateEvaluationCriterionCommandValidator() => RuleFor(x => x.Dto).Custom((dto, c) => { foreach (var problem in ContractorChecks.Problems(dto)) c.AddFailure(problem); });
    }

    // ───────── المقاولون ─────────

    public class GetContractorsHandler(ContractContext ctx) : IRequestHandler<GetContractorsQuery, List<ContractorResponseDto>>
    {
        public async Task<List<ContractorResponseDto>> Handle(GetContractorsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return [];
            var items = await ctx.Contracts.GetContractorsAsync(branch, request.IncludeInactive && ContractContext.CanManageContractors(viewer));
            return items.Select(ToDto).ToList();
        }
    }

    public class CreateContractorHandler(ContractContext ctx) : IRequestHandler<CreateContractorCommand, ContractorResponseDto>
    {
        public async Task<ContractorResponseDto> Handle(CreateContractorCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = ContractContext.ResolveBranch(viewer, dto.BranchId);
            var name = dto.Name.Trim();
            if (await ctx.Contracts.ContractorNameExistsAsync(branch, name)) throw new InvalidOperationException("يوجد مقاول بهذا الاسم في الفرع");

            var contractor = await ctx.Contracts.AddContractorAsync(new Contractor
            {
                BranchId = branch, Name = name, RegistrationNumber = ContractorChecks.Clean(dto.RegistrationNumber), ContactName = ContractorChecks.Clean(dto.ContactName),
                Phone = ContractorChecks.Clean(dto.Phone), Address = ContractorChecks.Clean(dto.Address), Notes = (dto.Notes ?? string.Empty).Trim(), IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Contractor, null, $"إضافة مقاول «{name}»"));
            return ToDto(contractor);
        }
    }

    public class UpdateContractorHandler(ContractContext ctx) : IRequestHandler<UpdateContractorCommand, ContractorResponseDto>
    {
        public async Task<ContractorResponseDto> Handle(UpdateContractorCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contractor = await ctx.Contracts.GetContractorAsync(request.Id) ?? throw new KeyNotFoundException("المقاول غير موجود");
            ContractRules.EnsureSameBranch(viewer, contractor.BranchId);

            var name = dto.Name.Trim();
            if (await ctx.Contracts.ContractorNameExistsAsync(contractor.BranchId, name, contractor.Id)) throw new InvalidOperationException("يوجد مقاول بهذا الاسم في الفرع");

            var details = Lines(
                Change("الاسم", contractor.Name, name),
                Change("السجل", contractor.RegistrationNumber, dto.RegistrationNumber),
                Change("المسؤول", contractor.ContactName, dto.ContactName),
                Change("الهاتف", contractor.Phone, dto.Phone),
                Change("العنوان", contractor.Address, dto.Address),
                Change("الملاحظات", contractor.Notes, dto.Notes),
                Change("فعّال", contractor.IsActive ? "نعم" : "لا", dto.IsActive ? "نعم" : "لا"));
            contractor.Name = name;
            contractor.RegistrationNumber = ContractorChecks.Clean(dto.RegistrationNumber);
            contractor.ContactName = ContractorChecks.Clean(dto.ContactName);
            contractor.Phone = ContractorChecks.Clean(dto.Phone);
            contractor.Address = ContractorChecks.Clean(dto.Address);
            contractor.Notes = (dto.Notes ?? string.Empty).Trim();
            contractor.IsActive = dto.IsActive;

            await ctx.Contracts.UpdateContractorAsync(contractor, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Contractor, contractor.Id, $"تعديل المقاول «{name}»", details));
            return ToDto(contractor);
        }
    }

    public class DeleteContractorHandler(ContractContext ctx) : IRequestHandler<DeleteContractorCommand, bool>
    {
        public async Task<bool> Handle(DeleteContractorCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var contractor = await ctx.Contracts.GetContractorAsync(request.Id) ?? throw new KeyNotFoundException("المقاول غير موجود");
            ContractRules.EnsureSameBranch(viewer, contractor.BranchId);
            if (await ctx.Contracts.ContractorInUseAsync(contractor.Id)) throw new InvalidOperationException("عليه عقود أو عروض أو تقييمات: لا يُحذف — عطّله بدلاً من ذلك");

            await ctx.Contracts.DeleteContractorAsync(contractor, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Contractor, contractor.Id, $"حذف مقاول «{contractor.Name}»"));
            return true;
        }
    }

    /// <summary>ملف المقاول: المراجَعة منها فقط، على عقود مشاريع يراها المستخدم، ولمن يملك «عرض التقييمات»</summary>
    public class GetContractorEvaluationsHandler(ContractContext ctx) : IRequestHandler<GetContractorEvaluationsQuery, List<ContractorEvaluationHistoryDto>>
    {
        public async Task<List<ContractorEvaluationHistoryDto>> Handle(GetContractorEvaluationsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            if (!viewer.IsSuperAdmin && !viewer.Has(AppPermissions.ViewContractorEvaluations))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض تقييمات المقاولين");
            var contractor = await ctx.Contracts.GetContractorAsync(request.ContractorId) ?? throw new KeyNotFoundException("المقاول غير موجود");
            ContractRules.EnsureSameBranch(viewer, contractor.BranchId);

            var items = await ctx.Contracts.GetContractorEvaluationsAsync(contractor.Id, ProjectRules.Visible(viewer));
            return items.Select(e => ToHistory(e, viewer)).ToList();
        }
    }

    // ───────── معايير التقييم ─────────

    public class GetEvaluationCriteriaHandler(ContractContext ctx) : IRequestHandler<GetEvaluationCriteriaQuery, List<EvaluationCriterionResponseDto>>
    {
        public async Task<List<EvaluationCriterionResponseDto>> Handle(GetEvaluationCriteriaQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var branch = viewer.IsOrganizationWide ? request.BranchId : viewer.User.BranchId;
            if (!viewer.IsOrganizationWide && branch == null) return [];
            var items = await ctx.Contracts.GetCriteriaAsync(branch, request.IncludeInactive && ContractContext.CanManageCriteria(viewer));
            return items.Select(ToDto).ToList();
        }
    }

    public class CreateEvaluationCriterionHandler(ContractContext ctx) : IRequestHandler<CreateEvaluationCriterionCommand, EvaluationCriterionResponseDto>
    {
        public async Task<EvaluationCriterionResponseDto> Handle(CreateEvaluationCriterionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var branch = ContractContext.ResolveBranch(viewer, dto.BranchId);
            var name = dto.Name.Trim();
            if (await ctx.Contracts.CriterionNameExistsAsync(branch, name)) throw new InvalidOperationException("يوجد معيار بهذا الاسم في الفرع");

            var criterion = await ctx.Contracts.AddCriterionAsync(new EvaluationCriterion
            {
                BranchId = branch, Name = name, Description = (dto.Description ?? string.Empty).Trim(), Weight = dto.Weight, SortOrder = dto.SortOrder, IsActive = dto.IsActive
            }, new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.EvaluationCriterion, null, $"إضافة معيار تقييم «{name}»"));
            return ToDto(criterion);
        }
    }

    public class UpdateEvaluationCriterionHandler(ContractContext ctx) : IRequestHandler<UpdateEvaluationCriterionCommand, EvaluationCriterionResponseDto>
    {
        public async Task<EvaluationCriterionResponseDto> Handle(UpdateEvaluationCriterionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var criterion = await ctx.Contracts.GetCriterionAsync(request.Id) ?? throw new KeyNotFoundException("المعيار غير موجود");
            ContractRules.EnsureSameBranch(viewer, criterion.BranchId);

            var name = dto.Name.Trim();
            if (await ctx.Contracts.CriterionNameExistsAsync(criterion.BranchId, name, criterion.Id)) throw new InvalidOperationException("يوجد معيار بهذا الاسم في الفرع");

            var details = Lines(
                Change("الاسم", criterion.Name, name),
                Change("الوصف", criterion.Description, dto.Description),
                Change("الوزن", criterion.Weight?.ToString("0.##"), dto.Weight?.ToString("0.##")),
                Change("فعّال", criterion.IsActive ? "نعم" : "لا", dto.IsActive ? "نعم" : "لا"));
            criterion.Name = name;
            criterion.Description = (dto.Description ?? string.Empty).Trim();
            criterion.Weight = dto.Weight;
            criterion.SortOrder = dto.SortOrder;
            criterion.IsActive = dto.IsActive;

            await ctx.Contracts.UpdateCriterionAsync(criterion, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.EvaluationCriterion, criterion.Id, $"تعديل معيار التقييم «{name}»", details));
            return ToDto(criterion);
        }
    }

    public class DeleteEvaluationCriterionHandler(ContractContext ctx) : IRequestHandler<DeleteEvaluationCriterionCommand, bool>
    {
        public async Task<bool> Handle(DeleteEvaluationCriterionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var criterion = await ctx.Contracts.GetCriterionAsync(request.Id) ?? throw new KeyNotFoundException("المعيار غير موجود");
            ContractRules.EnsureSameBranch(viewer, criterion.BranchId);
            if (await ctx.Contracts.CriterionInUseAsync(criterion.Id)) throw new InvalidOperationException("عليه درجات في تقييمات: لا يُحذف — عطّله بدلاً من ذلك");

            await ctx.Contracts.DeleteCriterionAsync(criterion, new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.EvaluationCriterion, criterion.Id, $"حذف معيار تقييم «{criterion.Name}»"));
            return true;
        }
    }
}
