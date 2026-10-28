using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.AssignedTasks;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.Diwan.DiwanSupport;

namespace Application.Features.Diwan
{
    // مرفقات المعاملات: الكتب الممسوحة والملحقات ومسودة الصادر. PDF وصور JPG/PNG ومستندات Office (قواعد مرفقات المهام نفسها:
    // النوع من محتوى الملف لا من الامتداد، والماكرو ممنوع)، حتى DiwanRules.MaxAttachments ملفاً، 10MB للملف.

    public record UploadCorrespondenceAttachmentsCommand(int Id, List<UploadedFileDto> Files) : IRequest<CorrespondenceResponseDto>;
    public record GetCorrespondenceAttachmentQuery(int AttachmentId) : IRequest<CorrespondenceAttachmentFileDto>;
    public record DeleteCorrespondenceAttachmentCommand(int AttachmentId) : IRequest<CorrespondenceResponseDto>;

    /// <summary>ملف مرفق للقراءة المتدفقة (عرض أو تنزيل)</summary>
    public sealed record CorrespondenceAttachmentFileDto(string FileName, string ContentType, Stream Content, bool Previewable);

    public class UploadCorrespondenceAttachmentsHandler(DiwanContext ctx) : IRequestHandler<UploadCorrespondenceAttachmentsCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(UploadCorrespondenceAttachmentsCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, request.Id);
            DiwanRules.EnsureCanAttach(viewer, c);

            if (request.Files.Count == 0) throw new InvalidOperationException("اختر ملفاً للإرفاق");
            if (c.Attachments.Count + request.Files.Count > DiwanRules.MaxAttachments)
                throw new InvalidOperationException($"الحد الأقصى {DiwanRules.MaxAttachments} ملفاً لكل معاملة (مرفق الآن {c.Attachments.Count})");
            foreach (var file in request.Files)
                if (TaskAttachmentRules.Problem(file) is string problem) throw new InvalidOperationException(problem);

            var now = DateTime.UtcNow;
            var created = new List<CorrespondenceAttachment>();
            foreach (var file in request.Files)
            {
                // في مجلد المرفقات أولاً إن كان مُفعَّلاً، وإلا في القاعدة
                var key = await ctx.Files.SaveAsync(FileAreas.Diwan, file.Data, ct);
                created.Add(new CorrespondenceAttachment
                {
                    CorrespondenceId = c.Id,
                    FileName = TaskAttachmentRules.SafeFileName(file.FileName),
                    ContentType = TaskAttachmentRules.DetectContentType(file.Data, file.FileName)!,
                    Size = file.Data.Length,
                    UploadedByUserId = viewer.Id,
                    UploadedAt = now,
                    StorageKey = key,
                    Content = key == null ? new CorrespondenceAttachmentContent { Data = file.Data } : null
                });
            }

            var names = string.Join("، ", created.Select(a => $"«{a.FileName}»"));
            await ctx.Diwan.AddAttachmentsAsync(c, created,
                new AuditEntry(viewer.Id, AuditActions.Attached, AuditEntities.Correspondence, c.Id, $"إرفاق ملفات بالمعاملة {Label(c)}", names));
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }

    public class GetCorrespondenceAttachmentHandler(DiwanContext ctx) : IRequestHandler<GetCorrespondenceAttachmentQuery, CorrespondenceAttachmentFileDto>
    {
        public async Task<CorrespondenceAttachmentFileDto> Handle(GetCorrespondenceAttachmentQuery request, CancellationToken ct)
        {
            var attachment = await ctx.Diwan.GetAttachmentAsync(request.AttachmentId) ?? throw new KeyNotFoundException("المرفق غير موجود");
            var viewer = await ctx.ViewerAsync();
            await ctx.LoadAsync(viewer, attachment.CorrespondenceId);   // حدّ المعاملة نفسها

            var content = await StoredFiles.OpenAsync(ctx.Files, attachment.StorageKey, () => ctx.Diwan.GetAttachmentDataAsync(attachment.Id));
            return new CorrespondenceAttachmentFileDto(attachment.FileName, attachment.ContentType, content, TaskAttachmentRules.IsPreviewable(attachment.ContentType));
        }
    }

    /// <summary>حذف مرفق: لمسودة الصادر فقط (من يحرّرها). مرفقات الوارد وما بعد الاعتماد أثر لا يُمحى.</summary>
    public class DeleteCorrespondenceAttachmentHandler(DiwanContext ctx) : IRequestHandler<DeleteCorrespondenceAttachmentCommand, CorrespondenceResponseDto>
    {
        public async Task<CorrespondenceResponseDto> Handle(DeleteCorrespondenceAttachmentCommand request, CancellationToken ct)
        {
            var attachment = await ctx.Diwan.GetAttachmentAsync(request.AttachmentId) ?? throw new KeyNotFoundException("المرفق غير موجود");
            var viewer = await ctx.ViewerAsync();
            var c = await ctx.LoadAsync(viewer, attachment.CorrespondenceId);
            if (!DiwanRules.IsOutgoingDraft(c)) throw new InvalidOperationException("المرفقات لا تُحذف إلا من مسودة الصادر");
            DiwanRules.EnsureCanEditDraft(viewer, c);

            var name = attachment.FileName;
            await ctx.Diwan.DeleteAttachmentAsync(c, attachment,
                new AuditEntry(viewer.Id, AuditActions.Deleted, AuditEntities.Correspondence, c.Id, $"حذف مرفق من المسودة {Label(c)}", $"«{name}»"));
            return await ctx.DetailAsync(viewer, c.Id);
        }
    }
}
