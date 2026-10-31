using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using static Application.Features.Contracts.ContractSupport;

namespace Application.Features.Contracts
{
    // تقييم المقاول: يعدّه مهندس الموقع (مسودة) ويرسله إلى مكتب العقود فيراجعه غيره ويحفظه في ملف المقاول، أو يعيده لاستكماله.
    // الدرجات يدوية بلا مجموع ولا تصنيف (المعايير وطريقة الدرجة لم تُعتمد رسمياً).

    public record CreateContractEvaluationCommand(int ContractId, ContractEvaluationRequestDto Dto) : IRequest<ContractDetailDto>;
    public record UpdateContractEvaluationCommand(int EvaluationId, ContractEvaluationRequestDto Dto) : IRequest<ContractDetailDto>;
    public record DeleteContractEvaluationCommand(int EvaluationId) : IRequest<ContractDetailDto>;
    public record SubmitContractEvaluationCommand(int EvaluationId, EvaluationStepRequestDto Dto) : IRequest<ContractDetailDto>;
    public record ReviewContractEvaluationCommand(int EvaluationId, EvaluationStepRequestDto Dto) : IRequest<ContractDetailDto>;
    public record ReturnContractEvaluationCommand(int EvaluationId, EvaluationStepRequestDto Dto) : IRequest<ContractDetailDto>;

    internal static class EvaluationInput
    {
        /// <summary>التحقق من الدرجات والملاحظات والتاريخ والوثيقة، وتحميل المعايير المطلوبة</summary>
        public static async Task<(Dictionary<int, EvaluationCriterion> Criteria, ProjectDocumentVersion? Version)> ResolveAsync(
            ContractContext ctx, Contract contract, ContractEvaluationRequestDto dto)
        {
            if (dto.EvaluationDate.Date > DateTime.Today) throw new InvalidOperationException("تاريخ التقييم لا يكون في المستقبل");
            if ((dto.Notes ?? string.Empty).Length > ContractRules.MaxEvaluationNotes) throw new InvalidOperationException($"الملاحظات لا تتجاوز {ContractRules.MaxEvaluationNotes} حرف");
            var inputs = dto.Scores.Select(s => new ContractRules.ScoreInput(s.CriterionId, s.Score, s.Note)).ToList();
            var criteria = await ctx.Contracts.GetCriteriaByIdsAsync(inputs.Select(s => s.CriterionId).Distinct().ToList());
            if (ContractRules.ValidateScores(inputs, criteria, contract.BranchId) is string problem) throw new InvalidOperationException(problem);
            return (criteria, await ctx.ResolveVersionAsync(dto.DocumentVersionId, contract.ProjectId));
        }
    }

    public class CreateContractEvaluationHandler(ContractContext ctx) : IRequestHandler<CreateContractEvaluationCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(CreateContractEvaluationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var contract = await ctx.LoadAsync(viewer, request.ContractId);
            ContractRules.EnsureCanEvaluate(viewer, contract);
            var (criteria, version) = await EvaluationInput.ResolveAsync(ctx, contract, dto);

            var now = DateTime.UtcNow;
            var evaluation = new ContractorEvaluation
            {
                ContractId = contract.Id, ContractorId = contract.ContractorId!.Value, EvaluatedByUserId = viewer.Id, EvaluationDate = dto.EvaluationDate.Date,
                Status = EvaluationStatus.Draft, Notes = (dto.Notes ?? string.Empty).Trim(), DocumentVersionId = version?.Id, CreatedAt = now, UpdatedAt = now,
                Scores = dto.Scores.Select(s => new EvaluationScore
                {
                    CriterionId = s.CriterionId, CriterionName = criteria[s.CriterionId].Name, Weight = criteria[s.CriterionId].Weight, Score = s.Score, Note = (s.Note ?? string.Empty).Trim()
                }).ToList()
            };
            await ctx.Contracts.AddEvaluationAsync(contract, evaluation,
                new AuditEntry(viewer.Id, AuditActions.Created, AuditEntities.ContractEvaluation, null,
                    $"مسودة تقييم مقاول على العقد {ContractContext.Label(contract)}", $"عدد المعايير: {evaluation.Scores.Count}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>تعديل المسودة: تُحدَّث درجات المعايير الموجودة وتُضاف الجديدة وتُحذف ما لم تُرسَل (اسم المعيار ووزنه يبقيان كما نُسخا)</summary>
    public class UpdateContractEvaluationHandler(ContractContext ctx) : IRequestHandler<UpdateContractEvaluationCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(UpdateContractEvaluationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (contract, evaluation) = await ctx.LoadEvaluationAsync(viewer, request.EvaluationId);
            ContractRules.EnsureCanEvaluate(viewer, contract);
            ContractRules.EnsureCanEditEvaluation(viewer, evaluation);
            var (criteria, version) = await EvaluationInput.ResolveAsync(ctx, contract, dto);

            var sent = dto.Scores.ToDictionary(s => s.CriterionId);
            var removed = evaluation.Scores.Where(s => !sent.ContainsKey(s.CriterionId)).ToList();
            foreach (var score in evaluation.Scores.Where(s => sent.ContainsKey(s.CriterionId)))
            {
                score.Score = sent[score.CriterionId].Score;
                score.Note = (sent[score.CriterionId].Note ?? string.Empty).Trim();
            }
            var existing = evaluation.Scores.Select(s => s.CriterionId).ToHashSet();
            foreach (var s in dto.Scores.Where(s => !existing.Contains(s.CriterionId)))
                evaluation.Scores.Add(new EvaluationScore
                {
                    EvaluationId = evaluation.Id, CriterionId = s.CriterionId, CriterionName = criteria[s.CriterionId].Name, Weight = criteria[s.CriterionId].Weight,
                    Score = s.Score, Note = (s.Note ?? string.Empty).Trim()
                });

            evaluation.EvaluationDate = dto.EvaluationDate.Date;
            evaluation.Notes = (dto.Notes ?? string.Empty).Trim();
            evaluation.DocumentVersionId = version?.Id;
            evaluation.ReviewNote = string.Empty;

            await ctx.Contracts.SaveEvaluationAsync(contract, evaluation, removed, dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Updated, AuditEntities.ContractEvaluation, evaluation.Id,
                    $"تعديل مسودة تقييم المقاول على العقد {ContractContext.Label(contract)}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    public class DeleteContractEvaluationHandler(ContractContext ctx) : IRequestHandler<DeleteContractEvaluationCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(DeleteContractEvaluationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (contract, evaluation) = await ctx.LoadEvaluationAsync(viewer, request.EvaluationId);
            ContractRules.EnsureCanEvaluate(viewer, contract);
            ContractRules.EnsureCanEditEvaluation(viewer, evaluation);

            await ctx.Contracts.DeleteEvaluationAsync(contract, evaluation,
                new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.ContractEvaluation, evaluation.Id,
                    $"حذف مسودة تقييم المقاول على العقد {ContractContext.Label(contract)}"));
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    public class SubmitContractEvaluationHandler(ContractContext ctx) : IRequestHandler<SubmitContractEvaluationCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(SubmitContractEvaluationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var (contract, evaluation) = await ctx.LoadEvaluationAsync(viewer, request.EvaluationId);
            ContractRules.EnsureCanSubmitEvaluation(viewer, contract, evaluation);

            evaluation.Status = EvaluationStatus.Submitted;
            evaluation.SubmittedAt = DateTime.UtcNow;
            evaluation.ReviewNote = string.Empty;
            await ctx.Contracts.SaveEvaluationAsync(contract, evaluation, [], request.Dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Submitted, AuditEntities.ContractEvaluation, evaluation.Id,
                    $"إرسال تقييم المقاول على العقد {ContractContext.Label(contract)} إلى مكتب العقود"));

            await ctx.NotifyAsync(await ctx.HoldersAsync(contract.Project, AppPermissions.ReviewContractorEvaluation), viewer.Id, contract,
                NotificationType.ContractEvaluationSubmitted, "تقييم مقاول بانتظار مراجعتك",
                $"أرسل {viewer.User.FullName} تقييم المقاول «{evaluation.Contractor?.Name ?? contract.Contractor?.Name}» على العقد {ContractContext.Label(contract)}");
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>المراجعة وحفظ التقييم في ملف المقاول (غير مُعدّه، حتى مدير النظام): المراجَع لا يُعدَّل بعدها</summary>
    public class ReviewContractEvaluationHandler(ContractContext ctx) : IRequestHandler<ReviewContractEvaluationCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(ReviewContractEvaluationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (contract, evaluation) = await ctx.LoadEvaluationAsync(viewer, request.EvaluationId);
            ContractRules.EnsureCanReviewEvaluation(viewer, contract, evaluation, dto.Note);

            evaluation.Status = EvaluationStatus.Reviewed;
            evaluation.ReviewedByUserId = viewer.Id;
            evaluation.ReviewedAt = DateTime.UtcNow;
            evaluation.ReviewNote = (dto.Note ?? string.Empty).Trim();
            await ctx.Contracts.SaveEvaluationAsync(contract, evaluation, [], dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Approved, AuditEntities.ContractEvaluation, evaluation.Id,
                    $"مراجعة تقييم المقاول على العقد {ContractContext.Label(contract)} وحفظه في ملف المقاول",
                    string.IsNullOrWhiteSpace(dto.Note) ? string.Empty : $"الملاحظة: {dto.Note.Trim()}"));

            await ctx.NotifyAsync([evaluation.EvaluatedByUserId], viewer.Id, contract, NotificationType.ContractEvaluationReviewed,
                "حُفظ تقييم أعددته", $"راجع {viewer.User.FullName} تقييم المقاول على العقد {ContractContext.Label(contract)} وحفظه في ملف المقاول");
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }

    /// <summary>إعادة التقييم المرسل إلى مُعدّه لاستكماله بسبب مكتوب: يعود مسودة</summary>
    public class ReturnContractEvaluationHandler(ContractContext ctx) : IRequestHandler<ReturnContractEvaluationCommand, ContractDetailDto>
    {
        public async Task<ContractDetailDto> Handle(ReturnContractEvaluationCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            var (contract, evaluation) = await ctx.LoadEvaluationAsync(viewer, request.EvaluationId);
            ContractRules.EnsureCanReturnEvaluation(viewer, contract, evaluation, dto.Note);

            evaluation.Status = EvaluationStatus.Draft;
            evaluation.SubmittedAt = null;
            evaluation.ReviewNote = dto.Note!.Trim();
            await ctx.Contracts.SaveEvaluationAsync(contract, evaluation, [], dto.RowVersion,
                new AuditEntry(viewer.Id, AuditActions.Returned, AuditEntities.ContractEvaluation, evaluation.Id,
                    $"إعادة تقييم المقاول على العقد {ContractContext.Label(contract)} لاستكماله", $"السبب: {dto.Note.Trim()}"));

            await ctx.NotifyAsync([evaluation.EvaluatedByUserId], viewer.Id, contract, NotificationType.ContractEvaluationReturned,
                "أُعيد إليك تقييم مقاول", $"أعاد {viewer.User.FullName} تقييم المقاول على العقد {ContractContext.Label(contract)}: {dto.Note.Trim()}");
            return await ctx.DetailAsync(viewer, contract.Id);
        }
    }
}
