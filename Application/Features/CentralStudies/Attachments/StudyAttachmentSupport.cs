using Application.Common;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Attachments
{
    /// <summary>الدوال المشتركة بين عمليات Attachments (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyAttachmentSupport
    {
        private readonly StudyKit _kit;

        public StudyAttachmentSupport(StudyKit kit)
        {
            _kit = kit;
        }

        public static StudyEntityType OwnerTypeOf(StudyAttachment a) =>
            a.RequestId != null ? StudyEntityType.Request
            : a.AssignmentId != null ? StudyEntityType.Assignment
            : a.ResponseId != null ? StudyEntityType.Response
            : a.StartOrderId != null ? StudyEntityType.StartOrder
            : a.ProjectId != null ? StudyEntityType.Project
            : a.DrawingId != null ? StudyEntityType.Drawing
            : StudyEntityType.Version;

        public static bool CanEdit(Viewer viewer, AttachmentOwner owner) =>
            !owner.Closed && viewer.Has(owner.EditPermission) && StudyRules.InScope(viewer, owner.BranchId)
            && (owner.EditDepartmentId == null || StudyRules.InDepartment(viewer, owner.EditDepartmentId));

        public async Task<AttachmentOwner> OwnerAsync(int type, int id)
        {
            var ownerType = (StudyEntityType)type;
            if (!StudyAttachmentRules.Owners.Contains(ownerType)) throw new ArgumentException("صاحب المرفق غير مدعوم");
            return await _kit.ResolveOwnerAsync(ownerType, id);
        }

        public static bool CanView(Viewer viewer, AttachmentOwner owner) =>
            viewer.Has(owner.ViewPermission) && StudyRules.InScope(viewer, owner.BranchId);

        /// <summary>
        /// مرفقات المشروع نفسه (2026-11-10) لصاحب الرؤية الكاملة ومن رفعها؛ ومرفقات الوثائق والمخططات والنسخ كما هي
        /// (مدير المشروع يرى مرفقات مشروعه كلها)
        /// </summary>
        public static bool CanSeeAttachment(Viewer viewer, AttachmentOwner owner, StudyAttachment attachment) =>
            owner.Type != StudyEntityType.Project
            || (owner.ProjectManagerUserId is int manager
                ? ProjectScope.For(viewer, manager).CanSeeProjectAttachment(attachment.UploadedByUserId)
                : attachment.UploadedByUserId == viewer.Id);

        public static int OwnerIdOf(StudyAttachment a) =>
            a.RequestId ?? a.AssignmentId ?? a.ResponseId ?? a.StartOrderId ?? a.ProjectId ?? a.DrawingId ?? a.VersionId ?? 0;
    }
}
