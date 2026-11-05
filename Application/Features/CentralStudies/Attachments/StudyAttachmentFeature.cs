using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Attachments
{
    // مرفقات فرع الدراسات — جدول واحد لكل المالكين (طلب، تكليف، رد، أمر بدء، مشروع، مخطط، نسخة معمارية).
    // بلا صلاحية جديدة: العرض بصلاحية عرض المالك والرفع والحذف بصلاحية تعديله (المخطط ونسخه لقسمه فقط)، داخل فرعه.
    // النوع يُكشف من محتوى الملف (MapFileRules): صور، PDF، Office، مخططات DWG/DXF، نصوص، أرشيف — والبرامج والسكربتات ممنوعة دائماً.
    // المحتوى في مجلد المرفقات على الخادم دائماً وبلا سقف للحجم (ملفات المخططات قد تبلغ جيجابايت — قرار المستخدم 2026-11-05):
    // يُرفع تدفقاً من جسم الطلب إلى ملف مؤقت في المجلد، يُفحص ثم يُعتمد، ولا يمر بالذاكرة.
    public record GetStudyAttachmentsQuery(int OwnerType, int OwnerId) : IRequest<List<StudyAttachmentDto>>;
    public record UploadStudyAttachmentCommand(int OwnerType, int OwnerId, int AttachmentTypeId, IUploadReader Upload) : IRequest<StudyAttachmentDto>;
    public record GetStudyAttachmentFileQuery(int Id) : IRequest<StudyAttachmentContentDto>;
    public record DeleteStudyAttachmentCommand(int Id) : IRequest<Unit>;

    public static class StudyAttachmentRules
    {
        /// <summary>لا سقف لحجم الملف (المساحة الحرة في المجلد هي الحد)؛ العدد لكل سجل محدود كي تبقى القوائم معقولة</summary>
        public const int MaxFilesPerOwner = 100;
        public static readonly string[] AllowedKinds = ["images", "pdf", "office", "cad", "text", "archives"];

        public static readonly StudyEntityType[] Owners =
        [
            StudyEntityType.Request, StudyEntityType.Assignment, StudyEntityType.Response, StudyEntityType.StartOrder,
            StudyEntityType.Project, StudyEntityType.Drawing, StudyEntityType.Version
        ];
    }

    public class UploadStudyAttachmentValidator : AbstractValidator<UploadStudyAttachmentCommand>
    {
        public UploadStudyAttachmentValidator()
        {
            RuleFor(x => x.OwnerType).Must(t => StudyAttachmentRules.Owners.Contains((StudyEntityType)t)).WithMessage("صاحب المرفق غير مدعوم");
            RuleFor(x => x.OwnerId).GreaterThan(0).WithMessage("حدد صاحب المرفق");
            RuleFor(x => x.AttachmentTypeId).GreaterThan(0).WithMessage("اختر نوع المرفق");
        }
    }

    public class StudyAttachmentHandler :
        IRequestHandler<GetStudyAttachmentsQuery, List<StudyAttachmentDto>>,
        IRequestHandler<UploadStudyAttachmentCommand, StudyAttachmentDto>,
        IRequestHandler<GetStudyAttachmentFileQuery, StudyAttachmentContentDto>,
        IRequestHandler<DeleteStudyAttachmentCommand, Unit>
    {
        private readonly IStudyAttachmentService _service;
        private readonly IStudyLookupService _lookups;
        private readonly IFileStore _files;
        private readonly StudyKit _kit;

        public StudyAttachmentHandler(IStudyAttachmentService service, IStudyLookupService lookups, IFileStore files, StudyKit kit)
        {
            _service = service;
            _lookups = lookups;
            _files = files;
            _kit = kit;
        }

        private static StudyEntityType OwnerTypeOf(StudyAttachment a) =>
            a.RequestId != null ? StudyEntityType.Request
            : a.AssignmentId != null ? StudyEntityType.Assignment
            : a.ResponseId != null ? StudyEntityType.Response
            : a.StartOrderId != null ? StudyEntityType.StartOrder
            : a.ProjectId != null ? StudyEntityType.Project
            : a.DrawingId != null ? StudyEntityType.Drawing
            : StudyEntityType.Version;

        private static int OwnerIdOf(StudyAttachment a) =>
            a.RequestId ?? a.AssignmentId ?? a.ResponseId ?? a.StartOrderId ?? a.ProjectId ?? a.DrawingId ?? a.VersionId ?? 0;

        private static void SetOwner(StudyAttachment a, StudyEntityType type, int id)
        {
            switch (type)
            {
                case StudyEntityType.Request: a.RequestId = id; break;
                case StudyEntityType.Assignment: a.AssignmentId = id; break;
                case StudyEntityType.Response: a.ResponseId = id; break;
                case StudyEntityType.StartOrder: a.StartOrderId = id; break;
                case StudyEntityType.Project: a.ProjectId = id; break;
                case StudyEntityType.Drawing: a.DrawingId = id; break;
                case StudyEntityType.Version: a.VersionId = id; break;
                default: throw new ArgumentException("صاحب المرفق غير مدعوم");
            }
        }

        private static bool CanView(Viewer viewer, AttachmentOwner owner) =>
            viewer.Has(owner.ViewPermission) && StudyRules.InScope(viewer, owner.BranchId);

        private static bool CanEdit(Viewer viewer, AttachmentOwner owner) =>
            !owner.Closed && viewer.Has(owner.EditPermission) && StudyRules.InScope(viewer, owner.BranchId)
            && (owner.EditDepartmentId == null || StudyRules.InDepartment(viewer, owner.EditDepartmentId));

        private async Task<AttachmentOwner> OwnerAsync(int type, int id)
        {
            var ownerType = (StudyEntityType)type;
            if (!StudyAttachmentRules.Owners.Contains(ownerType)) throw new ArgumentException("صاحب المرفق غير مدعوم");
            return await _kit.ResolveOwnerAsync(ownerType, id);
        }

        public async Task<List<StudyAttachmentDto>> Handle(GetStudyAttachmentsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var owner = await OwnerAsync(request.OwnerType, request.OwnerId);
            if (!CanView(viewer, owner)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض مرفقات هذا السجل");
            var canEdit = CanEdit(viewer, owner);
            return (await _service.GetForOwnerAsync(owner.Type, owner.Id)).Select(a =>
            {
                var dto = _kit.Mapper.Map<StudyAttachmentDto>(a);
                dto.CanDelete = canEdit;
                return dto;
            }).ToList();
        }

        public async Task<StudyAttachmentDto> Handle(UploadStudyAttachmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var owner = await OwnerAsync(request.OwnerType, request.OwnerId);
            if (!CanEdit(viewer, owner))
                throw new UnauthorizedAccessException(owner.Closed ? "السجل ملغى — لا تُضاف مرفقات" : "لا تملك صلاحية الإرفاق بهذا السجل");
            var type = await _lookups.GetAttachmentTypeAsync(request.AttachmentTypeId);
            if (type is not { IsActive: true }) throw new KeyNotFoundException("نوع المرفق غير موجود أو غير فعّال");
            if (await _service.CountForOwnerAsync(owner.Type, owner.Id) >= StudyAttachmentRules.MaxFilesPerOwner)
                throw new InvalidOperationException($"المرفقات {StudyAttachmentRules.MaxFilesPerOwner} ملفاً على الأكثر لكل سجل");

            // كل الفحوص السابقة قبل استقبال أي بايت؛ ثم الملف تدفقاً إلى ملف مؤقت في المجلد، يُفحص نوعه من محتواه ثم يُعتمد
            string? originalName = null;
            await using var staged = await _files.StageAsync(FileAreas.Studies,
                async (destination, token) => originalName = await request.Upload.CopyFileToAsync(destination, token), ct);
            if (originalName == null || staged.Size == 0) throw new InvalidOperationException("اختر ملفاً غير فارغ");

            var name = MapFileRules.SafeFileName(originalName);
            MapFileRules.Detected? detected;
            await using (var content = staged.OpenRead()) detected = MapFileRules.Detect(content, name);
            if (detected == null || !detected.Kinds.Any(k => StudyAttachmentRules.AllowedKinds.Contains(k)))
                throw new InvalidOperationException("صيغة الملف غير مسموحة — المسموح: صور، PDF، مستندات Office، مخططات DWG/DXF، نصوص، أرشيف مضغوط");

            // الملف أولاً ثم السجل: إن فشل حفظ السجل بقي ملف يتيم يحذفه المنظّف الدوري
            await staged.CommitAsync();
            var attachment = new StudyAttachment
            {
                AttachmentTypeId = type.Id,
                UploadedByUserId = viewer.Id,
                FileName = name,
                ContentType = detected.ContentType,
                Size = staged.Size,
                StorageKey = staged.Key,
                UploadedAt = DateTime.Now
            };
            SetOwner(attachment, owner.Type, owner.Id);
            await _service.AddAsync(attachment);
            await _kit.LogAsync(owner.RequestId, StudyEntityType.Attachment, attachment.Id, StudyLogAction.Created,
                $"أرفق «{name}» ({type.Name}) بـ{StudyRules.Name(owner.Type)}");

            var dto = _kit.Mapper.Map<StudyAttachmentDto>(await _service.GetByIdAsync(attachment.Id));
            dto.CanDelete = true;
            return dto;
        }

        public async Task<StudyAttachmentContentDto> Handle(GetStudyAttachmentFileQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var attachment = await _service.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("المرفق غير موجود");
            var owner = await _kit.ResolveOwnerAsync(OwnerTypeOf(attachment), OwnerIdOf(attachment));
            if (!CanView(viewer, owner)) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذا المرفق");
            var stream = _files.OpenRead(attachment.StorageKey);
            return new StudyAttachmentContentDto(attachment.FileName, attachment.ContentType, stream, MapFileRules.IsPreviewable(attachment.ContentType));
        }

        public async Task<Unit> Handle(DeleteStudyAttachmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var attachment = await _service.GetByIdAsync(request.Id) ?? throw new KeyNotFoundException("المرفق غير موجود");
            var owner = await _kit.ResolveOwnerAsync(OwnerTypeOf(attachment), OwnerIdOf(attachment));
            if (!CanEdit(viewer, owner)) throw new UnauthorizedAccessException("لا تملك صلاحية حذف هذا المرفق");
            var name = attachment.FileName;
            await _service.DeleteAsync(attachment);   // الملف في المجلد يحذفه المنظّف الدوري
            await _kit.LogAsync(owner.RequestId, StudyEntityType.Attachment, request.Id, StudyLogAction.Deleted,
                $"حذف المرفق «{name}» من {StudyRules.Name(owner.Type)}");
            return Unit.Value;
        }
    }
}
