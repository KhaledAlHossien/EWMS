using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.DiwanTransactions
{
    /// <summary>
    /// الدوال المشتركة بين عمليات معاملة الديوان (تحميل السجل بحدّه، التحويل إلى DTO وأعلامه، تطبيق النموذج).
    /// الحدّ كباقي مستندات الدراسة: StudyRules.InScope (فرع الدراسة، والمؤسسة كلها لنظرة المؤسسة ومدير النظام).
    /// </summary>
    public class StudyDiwanTransactionSupport
    {
        private readonly IStudyDiwanTransactionService _service;
        private readonly StudyKit _kit;

        public StudyDiwanTransactionSupport(IStudyDiwanTransactionService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        public StudyDiwanTransactionDto ToDto(StudyDiwanTransaction e, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyDiwanTransactionDto>(e);
            var inScope = StudyRules.InScope(viewer, e.Response.Assignment.Request.BranchId);
            var startOrderOpen = e.Response.StartOrder is { } order && order.Status != StudyDocumentStatus.Cancelled;
            // تُعدَّل وتُرسل مسودةً أو بعد رفضها، والرد معتمد (بعد الرفض المركزي ينتظر اعتماد الرد المعدّل أولاً)
            dto.CanEdit = inScope && viewer.Has(StudyPermissions.EditDiwan) && StudyRules.IsEditable(e.Status)
                          && e.Response.Status == StudyDocumentStatus.Approved;
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has(StudyPermissions.DeleteDiwan) && e.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && e.Status is not (StudyDocumentStatus.Draft or StudyDocumentStatus.Cancelled) && !startOrderOpen;
            dto.CanCreateStartOrder = inScope && viewer.Has("CreateStudyStartOrder") && ReadyForStartOrder(e) && e.Response.StartOrder == null;
            return dto;
        }

        /// <summary>أمر البدء يصدر من معاملة معتمدة بقرار موافقة مركزي، والتوصية الفنية ليست «غير متاح»</summary>
        public static bool ReadyForStartOrder(StudyDiwanTransaction e) =>
            e.Status == StudyDocumentStatus.Approved && e.CentralDecision == StudyDiwanDecision.Approved
            && e.Response.Status == StudyDocumentStatus.Approved && e.Response.Recommendation != StudyRecommendation.NotAvailable;

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
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.DiwanTransaction, entity.Id, ApprovalOwner.Of(entity));
            return dto;
        }

        /// <summary>بعد الرفض المركزي يعود الرد إلى القسم المتابع: لا تُعدَّل المعاملة ولا تُرسل قبل اعتماد الرد المعدّل</summary>
        public static void EnsureResponseApproved(StudyDiwanTransaction e)
        {
            if (e.Response.Status != StudyDocumentStatus.Approved)
                throw new InvalidOperationException("إشعار الرد عاد إلى القسم المتابع للتعديل — تُحدَّث المعاملة بعد اعتماده من جديد");
        }

        public static void Apply(StudyDiwanTransactionFormDto dto, StudyDiwanTransaction e)
        {
            e.DispatchNumber = dto.DispatchNumber.Trim();
            e.DispatchDate = dto.DispatchDate.Date;
            e.ReplyNumber = StudyRules.Clean(dto.ReplyNumber);
            e.ReplyDate = dto.ReplyDate?.Date;
            e.CentralDecision = (StudyDiwanDecision?)dto.CentralDecision;
            e.Notes = StudyRules.Clean(dto.Notes);
        }
    }
}
