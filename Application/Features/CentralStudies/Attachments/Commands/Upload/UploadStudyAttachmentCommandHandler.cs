using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Attachments.Commands.Upload
{
    public class UploadStudyAttachmentCommandHandler : IRequestHandler<UploadStudyAttachmentCommand, StudyAttachmentDto>
    {
        private readonly IStudyAttachmentService _service;
        private readonly IStudyLookupService _lookups;
        private readonly IFileStore _files;
        private readonly StudyKit _kit;
        private readonly StudyAttachmentSupport _support;

        public UploadStudyAttachmentCommandHandler(IStudyAttachmentService service, IStudyLookupService lookups, IFileStore files, StudyKit kit, StudyAttachmentSupport support)
        {
            _service = service;
            _lookups = lookups;
            _files = files;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyAttachmentDto> Handle(UploadStudyAttachmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var owner = await _support.OwnerAsync(request.OwnerType, request.OwnerId);
            if (!StudyAttachmentSupport.CanEdit(viewer, owner))
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
    }
}
