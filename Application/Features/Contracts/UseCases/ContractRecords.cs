using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Contracts.ContractSupport;

namespace Application.Features.Contracts
{
    // سجلات العقد: الملاحق المعتمدة، اللجنة الفنية والمالية، المبالغ المحتجزة وقراراتها. الملاحق والمحتجزات تُضاف ولا تُعدَّل (ADR-0007).

    public record AddContractAmendmentCommand(int ContractId, ContractAmendmentRequestDto Dto) : IRequest<ContractDetailDto>;
    public record CreateCommitteeCommand(int ContractId, CommitteeRequestDto Dto) : IRequest<ContractDetailDto>;
    public record UpdateCommitteeCommand(int CommitteeId, CommitteeRequestDto Dto) : IRequest<ContractDetailDto>;
    public record AddRetentionCommand(int ContractId, RetentionRequestDto Dto) : IRequest<ContractDetailDto>;
    public record DecideRetentionCommand(int EntryId, RetentionDecisionRequestDto Dto) : IRequest<ContractDetailDto>;

    public class AddContractAmendmentHandler(ContractContext ctx) : IRequestHandler<AddContractAmendmentCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(AddContractAmendmentCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.ContractId);
            ContractRules.EnsureCanAddAmendment(viewer, contract, dto.Title, dto.Description, dto.ValueChange, dto.DurationChangeDays, dto.ApprovedAt, DateTime.Today);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, contract.ProjectId);

            var number = ContractRules.NextAmendmentNumber(contract);
            await ctx.Contracts.AddAmendmentAsync(contract, new ContractAmendment
            {
                ContractId = contract.Id, Number = number, Title = dto.Title.Trim(), Description = (dto.Description ?? string.Empty).Trim(),
                ValueChange = dto.ValueChange, DurationChangeDays = dto.DurationChangeDays, ApprovedAt = dto.ApprovedAt.Date, DocumentVersionId = version?.Id,
                RecordedByUserId = viewer.Id, RecordedAt = DateTime.UtcNow
            }, new AuditEntry(viewer.Id, AuditActions.Recorded, AuditEntities.Contract, contract.Id,
                $"ملحق رقم {number} على العقد {ContractContext.Label(contract)}: {dto.Title.Trim()}",
                Lines(dto.ValueChange is decimal v ? $"أثره على القيمة: {Money(v, contract.Currency)}" : null,
                    dto.DurationChangeDays is int d ? $"أثره على المدة: {d} يوم" : null, version is null ? null : "مرفق بوثيقة من الإضبارة")));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    internal static class CommitteeInput
    {
        /// <summary>
        /// الأعضاء والتشكيل بعد التحقق: الموظفون نشطون ومن فرع المشروع، والرئيس واحد، والتاريخ معقول — وتُحمَّل أسماؤهم لعرضها.
        /// </summary>
        public static async Task<(List<CommitteeMember> Members, ProjectDocumentVersion? Decision)> ResolveAsync(ContractContext ctx, Contract contract, CommitteeRequestDto dto)
        {
            var inputs = dto.Members.Select(m => new ContractRules.MemberInput(m.UserId, m.ExternalName, m.Capacity, m.IsChair)).ToList();
            if (ContractRules.ValidateMembers(inputs) is string members) throw new InvalidOperationException(members);
            if (ContractRules.ValidateCommittee(dto.DecisionNumber, dto.Recommendations, dto.FormedAt, DateTime.Today) is string committee) throw new InvalidOperationException(committee);

            var result = new List<CommitteeMember>();
            foreach (var m in dto.Members)
            {
                User? user = null;
                if (m.UserId is int id)
                {
                    user = await ctx.Users.GetByIdAsync(id) ?? throw new KeyNotFoundException("موظف غير موجود");
                    if (!user.IsActive) throw new InvalidOperationException($"حساب «{user.FullName}» معطّل");
                    if (!OrganizationRole.InBranch(user, contract.BranchId)) throw new InvalidOperationException($"«{user.FullName}» من فرع آخر");
                }
                result.Add(new CommitteeMember
                {
                    UserId = user?.Id, User = user, ExternalName = user == null ? m.ExternalName!.Trim() : null, Capacity = m.Capacity.Trim(), IsChair = m.IsChair
                });
            }
            return (result, await ctx.ResolveVersionAsync(dto.DecisionVersionId, contract.ProjectId));
        }

        public static string Describe(IEnumerable<CommitteeMember> members) =>
            string.Join("، ", members.OrderByDescending(m => m.IsChair).Select(m => $"{(m.User?.FullName ?? m.ExternalName)} ({m.Capacity}{(m.IsChair ? "، رئيس" : string.Empty)})"));
    }

    public class CreateCommitteeHandler(ContractContext ctx) : IRequestHandler<CreateCommitteeCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(CreateCommitteeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.ContractId);
            ContractRules.EnsureCanManageCommittee(viewer, contract);
            var (members, decision) = await CommitteeInput.ResolveAsync(ctx, contract, dto);

            var committee = new Committee
            {
                ProjectId = contract.ProjectId, Kind = CommitteeKind.TechnicalFinancial, ContractId = contract.Id, FormedAt = dto.FormedAt.Date,
                DecisionNumber = string.IsNullOrWhiteSpace(dto.DecisionNumber) ? null : dto.DecisionNumber.Trim(), DecisionVersionId = decision?.Id,
                Recommendations = (dto.Recommendations ?? string.Empty).Trim(), CreatedByUserId = viewer.Id, CreatedAt = DateTime.UtcNow, Members = members
            };
            await ctx.Contracts.SaveCommitteeAsync(contract, committee, isNew: true, [], [], null,
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.Contract, contract.Id,
                    $"تشكيل اللجنة الفنية والمالية للعقد {ContractContext.Label(contract)}", $"الأعضاء: {CommitteeInput.Describe(members)}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>تعديل اللجنة: يُستبدل أعضاؤها بالقائمة المرسلة، والأثر يحفظ ما كان وما صار (سجل التعديلات)</summary>
    public class UpdateCommitteeHandler(ContractContext ctx) : IRequestHandler<UpdateCommitteeCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(UpdateCommitteeCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (contract, committee) = await ctx.LoadCommitteeAsync(viewer, request.CommitteeId);
            ContractRules.EnsureCanManageCommittee(viewer, contract);
            var (members, decision) = await CommitteeInput.ResolveAsync(ctx, contract, dto);

            var removed = committee.Members.ToList();
            var details = Lines(
                $"الأعضاء قبل: {CommitteeInput.Describe(removed)}", $"الأعضاء بعد: {CommitteeInput.Describe(members)}",
                Change("رقم القرار", committee.DecisionNumber, dto.DecisionNumber), Change("التوصيات", committee.Recommendations, dto.Recommendations),
                Change("تاريخ التشكيل", committee.FormedAt.ToString("yyyy-MM-dd"), dto.FormedAt.ToString("yyyy-MM-dd")));

            committee.FormedAt = dto.FormedAt.Date;
            committee.DecisionNumber = string.IsNullOrWhiteSpace(dto.DecisionNumber) ? null : dto.DecisionNumber.Trim();
            committee.DecisionVersionId = decision?.Id;
            committee.Recommendations = (dto.Recommendations ?? string.Empty).Trim();
            foreach (var member in members) member.CommitteeId = committee.Id;

            await ctx.Contracts.SaveCommitteeAsync(contract, committee, isNew: false, members, removed, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.Contract, contract.Id,
                    $"تعديل اللجنة الفنية والمالية للعقد {ContractContext.Label(contract)}", details));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    public class AddRetentionHandler(ContractContext ctx) : IRequestHandler<AddRetentionCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(AddRetentionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.ContractId);
            ContractRules.EnsureCanRecordRetention(viewer, contract, dto.Amount, dto.WithheldAt, dto.Note, DateTime.Today);

            await ctx.Contracts.AddRetentionAsync(contract, new RetentionEntry
            {
                ContractId = contract.Id, Amount = dto.Amount, WithheldAt = dto.WithheldAt.Date, Note = (dto.Note ?? string.Empty).Trim(),
                RecordedByUserId = viewer.Id, RecordedAt = DateTime.UtcNow
            }, new AuditEntry(viewer.Id, AuditActions.Recorded, AuditEntities.Contract, contract.Id,
                $"تسجيل مبلغ محتجز على العقد {ContractContext.Label(contract)}", $"المبلغ: {Money(dto.Amount, contract.Currency)} بتاريخ {dto.WithheldAt:yyyy-MM-dd}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>قرار في مبلغ محتجز بالاعتماد الرسمي: إفراج (نهائي) أو استمرار احتجاز، بجهة الاعتماد ووثيقة مؤيدة اختيارية</summary>
    public class DecideRetentionHandler(ContractContext ctx) : IRequestHandler<DecideRetentionCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(DecideRetentionCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (contract, entry) = await ctx.LoadRetentionAsync(viewer, request.EntryId);
            if (!ContractRules.IsValidRetentionDecision(dto.Decision)) throw new InvalidOperationException("القرار إفراج أو استمرار احتجاز");
            var kind = (RetentionDecisionKind)dto.Decision;
            ContractRules.EnsureCanDecideRetention(viewer, contract, entry, kind, dto.DecidedAt, dto.ApprovingAuthority, dto.Note, DateTime.Today);
            var version = await ctx.ResolveVersionAsync(dto.DocumentVersionId, contract.ProjectId);

            await ctx.Contracts.AddRetentionDecisionAsync(contract, new RetentionDecision
            {
                RetentionEntryId = entry.Id, Decision = kind, DecidedAt = dto.DecidedAt.Date, ApprovingAuthority = dto.ApprovingAuthority.Trim(),
                Note = (dto.Note ?? string.Empty).Trim(), DocumentVersionId = version?.Id, RecordedByUserId = viewer.Id, RecordedAt = DateTime.UtcNow
            }, new AuditEntry(viewer.Id, AuditActions.Decided, AuditEntities.Contract, contract.Id,
                $"{ContractRules.RetentionDecisionAr(kind)} للمبلغ المحتجز ({Money(entry.Amount, contract.Currency)}) في العقد {ContractContext.Label(contract)}",
                Lines($"جهة الاعتماد: {dto.ApprovingAuthority.Trim()}", $"تاريخ القرار: {dto.DecidedAt:yyyy-MM-dd}", string.IsNullOrWhiteSpace(dto.Note) ? null : $"الملاحظة: {dto.Note.Trim()}")));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }
}
