using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.DiwanTransactions
{
    /// <summary>
    /// الدوال المشتركة بين عمليات معاملة الديوان (تحميل السجل بحدّه، التحويل إلى DTO وأعلامه، تطبيق النموذج، نقل المرحلة،
    /// وتوقيع رئيس الفرع). الحدّ كباقي مستندات الدراسة: StudyRules.InScope (فرع الدراسة، والمؤسسة كلها لنظرة المؤسسة ومدير النظام).
    /// المسار (طلب الواجهة 2026-11-28): الديوان يسجّل ويرسل ← رئيس الفرع يحيل بموافقة أو رفض أو يعيد ← مدير المشاريع يصدر أمر البدء
    /// أو يختار تعديلاً تخطيطياً أو فنياً للرد ← بعد اعتماد الرد المعدّل يسجّل الديوان صادراً جديداً على المعاملة نفسها.
    /// </summary>
    public class StudyDiwanTransactionSupport
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly IStudyApprovalService _approvals;
        private readonly IUserSignatureService _signatures;
        private readonly StudyKit _kit;

        public StudyDiwanTransactionSupport(IStudyDiwanTransactionService service, IStudyApprovalService approvals,
            IUserSignatureService signatures, StudyKit kit)
        {
            _service = service;
            _approvals = approvals;
            _signatures = signatures;
            _kit = kit;
        }

        /// <summary>مراحل يعمل فيها موظف الديوان على المعاملة (تعديل، مرفقات)</summary>
        public static bool AtDiwan(StudyDiwanStage stage) => StudyRules.AtDiwan(stage);

        /// <summary>مراحل تُرسل منها إلى رئيس الفرع (من «جاهزة لصادر جديد» يُسجَّل الصادر أولاً)</summary>
        public static bool CanBeSubmitted(StudyDiwanStage stage) =>
            stage is StudyDiwanStage.Registering or StudyDiwanStage.ReturnedToDiwan;

        public StudyDiwanTransactionDto ToDto(StudyDiwanTransaction e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyDiwanTransactionDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Response.Assignment.Request.BranchId);
            var startOrderOpen = e.Response.StartOrder is { } order && order.Status != StudyDocumentStatus.Cancelled;
            var responseApproved = e.Response.Status == StudyDocumentStatus.Approved;
            var editor = inScope && viewer.Has(StudyPermissions.EditDiwan) && responseApproved;
            dto.CanEdit = editor && AtDiwan(e.Stage);
            dto.CanSubmit = editor && CanBeSubmitted(e.Stage);
            dto.CanDelete = inScope && viewer.Has(StudyPermissions.DeleteDiwan) && e.Stage == StudyDiwanStage.Registering && e.Rounds.Count == 0;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments) && e.Stage != StudyDiwanStage.Cancelled
                            && (e.Stage != StudyDiwanStage.Registering || e.Rounds.Count > 0) && !startOrderOpen;
            dto.CanRefer = inScope && viewer.Has(StudyPermissions.SignAsBranchHead) && e.Stage == StudyDiwanStage.AtBranchHead;
            dto.CanReturn = dto.CanRefer;
            dto.CanRequestRevision = inScope && viewer.Has(StudyPermissions.HandleDiwanRejection)
                                     && e.Stage == StudyDiwanStage.CentralRejected && responseApproved;
            dto.CanCreateStartOrder = inScope && viewer.Has("CreateStudyStartOrder") && ReadyForStartOrder(e) && e.Response.StartOrder == null;
            return dto;
        }

        /// <summary>أمر البدء يصدر من معاملة أحالها رئيس الفرع بموافقة مركزية، والتوصية الفنية ليست «غير متاح»</summary>
        public static bool ReadyForStartOrder(StudyDiwanTransaction e) =>
            e.Stage == StudyDiwanStage.CentralApproved && e.CentralDecision == StudyDiwanDecision.Approved
            && e.Response.Status == StudyDocumentStatus.Approved && e.Response.Recommendation != StudyRecommendation.NotAvailable;

        /// <summary>ينقل المعاملة إلى مرحلة ويضبط حالتها العامة معها</summary>
        public static void MoveTo(StudyDiwanTransaction e, StudyDiwanStage stage)
        {
            e.Stage = stage;
            e.Status = StudyRules.StatusOf(stage);
            e.UpdatedAt = DateTime.Now;
        }

        public async Task<StudyDiwanTransaction> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("معاملة الديوان غير موجودة");
            StudyRules.EnsureInScope(viewer, entity.Response.Assignment.Request.BranchId);
            return entity;
        }

        public async Task<StudyDiwanTransactionDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Response.Assignment.RequestId);
            dto.Approvals = await _kit.ApprovalsAsync(ApprovalOwner.Of(entity));
            await _kit.AddDiwanWhoIsNextAsync(dto, entity);
            return dto;
        }

        public static void EnsureStage(StudyDiwanTransaction e, StudyDiwanStage expected, string message)
        {
            if (e.Stage != expected) throw new InvalidOperationException($"{message} (المعاملة الآن: {StudyRules.Name(e.Stage)})");
        }

        /// <summary>الصادر والرد يُسجَّلان والرد معتمد؛ أثناء تعديله بعد رفض مركزي لا تُعدَّل المعاملة</summary>
        public static void EnsureResponseApproved(StudyDiwanTransaction e)
        {
            if (e.Response.Status != StudyDocumentStatus.Approved)
                throw new InvalidOperationException("إشعار الرد قيد التعديل — تُحدَّث المعاملة بعد اعتماده من جديد");
        }

        public static void EnsureAtDiwan(StudyDiwanTransaction e)
        {
            if (!AtDiwan(e.Stage))
                throw new InvalidOperationException($"المعاملة ليست لدى الديوان الآن ({StudyRules.Name(e.Stage)})");
        }

        public static void Apply(StudyDiwanTransactionFormDto dto, StudyDiwanTransaction e)
        {
            e.DispatchNumber = dto.DispatchNumber.Trim();
            e.DispatchDate = dto.DispatchDate.Date;
            e.ReplyNumber = StudyRules.Clean(dto.ReplyNumber);
            e.ReplyDate = dto.ReplyDate?.Date;
            e.Notes = StudyRules.Clean(dto.Notes);
        }

        /// <summary>
        /// يوقّع رئيس الفرع سطره على المعاملة (إحالة = موافقة على السطر، إعادة = رفضه) بتوقيعه الحالي وصفته — في السياق نفسه،
        /// فيحفظه حفظ المعاملة. يفحص صلاحية الصفة وحدّها كما يفحصها التوقيع العام.
        /// </summary>
        public async Task SignBranchHeadAsync(StudyDiwanTransaction e, Viewer viewer, StudyDecision decision, string? note)
        {
            var rows = await _approvals.GetTrackedForOwnerAsync(StudyEntityType.DiwanTransaction, e.Id);
            var row = rows.FirstOrDefault(r => r.SignerRole == StudySignerRole.BranchHead && r.Decision == StudyDecision.Pending)
                      ?? throw new InvalidOperationException("لا سطر توقيع معلّقاً لرئيس الفرع على المعاملة — أعد إرسالها");
            if (!StudyKit.CanDecide(viewer, row, rows, ApprovalOwner.Of(e)))
                throw new UnauthorizedAccessException("تحيل المعاملة أو تعيدها بصفة رئيس الفرع، في فرع الدراسة");
            row.Decision = decision;
            row.SignerUserId = viewer.Id;
            row.JobTitle = viewer.User.Role?.Name;
            row.SignatureId = await _signatures.GetCurrentIdAsync(viewer.Id);
            row.DecidedAt = DateTime.Now;
            row.Note = StudyRules.Clean(note);
        }
    }
}
