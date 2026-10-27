using System.Security.Cryptography;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.MapLayers;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.Projects
{
    // رفع وثائق المشاريع بأجزاء: الصلاحية UploadProjectDocument في المتحكم، وكل جلسة ملك من بدأها وحده،
    // وحدّ المشروع (ProjectRules.Visible) يُفحص عند البدء وعند الإنهاء.
    public record StartProjectUploadCommand(StartProjectUploadRequestDto Dto) : IRequest<ProjectUploadResponseDto>;
    public record WriteProjectChunkCommand(Guid Id, long Offset, long Length, Stream Body) : IRequest<ProjectUploadResponseDto>;
    public record GetProjectUploadQuery(Guid Id) : IRequest<ProjectUploadResponseDto>;
    public record ListMyProjectUploadsQuery(int? ProjectId) : IRequest<List<ProjectUploadResponseDto>>;
    public record CompleteProjectUploadCommand(Guid Id) : IRequest<ProjectUploadResponseDto>;
    public record CancelProjectUploadCommand(Guid Id) : IRequest<Unit>;

    public class StartProjectUploadCommandValidator : AbstractValidator<StartProjectUploadCommand>
    {
        public StartProjectUploadCommandValidator()
        {
            RuleFor(x => x.Dto.ProjectId).GreaterThan(0).WithMessage("المشروع مطلوب");
            RuleFor(x => x.Dto.FileName).NotEmpty().WithMessage("اسم الملف مطلوب").MaximumLength(ProjectUploadRules.MaxFileNameLength).WithMessage("اسم الملف طويل");
            RuleFor(x => x.Dto.Title).MaximumLength(200).WithMessage("عنوان الوثيقة لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Source).MaximumLength(200).WithMessage("مصدر الوثيقة لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
            RuleFor(x => x.Dto.Kind).Must(k => k == null || ProjectRules.IsValidKind(k.Value)).WithMessage("نوع الوثيقة غير صحيح");
        }
    }

    public class ProjectUploadHandler :
        IRequestHandler<StartProjectUploadCommand, ProjectUploadResponseDto>,
        IRequestHandler<WriteProjectChunkCommand, ProjectUploadResponseDto>,
        IRequestHandler<GetProjectUploadQuery, ProjectUploadResponseDto>,
        IRequestHandler<ListMyProjectUploadsQuery, List<ProjectUploadResponseDto>>,
        IRequestHandler<CompleteProjectUploadCommand, ProjectUploadResponseDto>,
        IRequestHandler<CancelProjectUploadCommand, Unit>
    {
        private readonly IProjectUploadService _uploads;
        private readonly IProjectUploadFiles _files;
        private readonly IProjectService _projects;
        private readonly IProjectDocumentService _documents;
        private readonly IProjectFileSettings _settings;
        private readonly IProjectUploadSignal _signal;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public ProjectUploadHandler(IProjectUploadService uploads, IProjectUploadFiles files, IProjectService projects,
            IProjectDocumentService documents, IProjectFileSettings settings, IProjectUploadSignal signal,
            IUserService users, IUserPermissionService permissions)
        {
            _uploads = uploads;
            _files = files;
            _projects = projects;
            _documents = documents;
            _settings = settings;
            _signal = signal;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> ViewerAsync() => Viewer.CurrentAsync(_users, _permissions);

        /// <summary>الجلسة المطلوبة وهي لصاحبها (غير الصاحب يُرفض ولو كان مدير النظام)</summary>
        private async Task<ProjectUpload> OwnedAsync(Viewer viewer, Guid id)
        {
            var upload = await _uploads.GetAsync(id) ?? throw new KeyNotFoundException("جلسة الرفع غير موجودة أو انتهت");
            if (upload.UserId != viewer.Id) throw new UnauthorizedAccessException("هذا الرفع ليس لك");
            return upload;
        }

        private async Task<Project> OpenVisibleProjectAsync(Viewer viewer, int projectId)
        {
            var project = await _projects.GetByIdAsync(projectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            ProjectRules.EnsureView(viewer, project);
            ProjectRules.EnsureOpen(project);
            return project;
        }

        public async Task<ProjectUploadResponseDto> Handle(StartProjectUploadCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var dto = request.Dto;
            await OpenVisibleProjectAsync(viewer, dto.ProjectId);

            var title = (dto.Title ?? string.Empty).Trim();
            var kind = dto.Kind is int k ? (ProjectDocumentKind)k : ProjectDocumentKind.Other;
            if (dto.DocumentId is int documentId)
            {
                var document = await _documents.GetDocumentAsync(documentId);
                if (document == null || document.ProjectId != dto.ProjectId) throw new KeyNotFoundException("الوثيقة غير موجودة في هذا المشروع");
                title = document.Title;
                kind = document.Kind;
            }
            else if (title.Length == 0)
                throw new InvalidOperationException("عنوان الوثيقة مطلوب");

            var fileName = MapFileRules.SafeFileName(dto.FileName);
            if (ProjectUploadRules.ValidateStart(fileName, dto.Size, _settings.EffectiveMaxBytes, _settings.UsesFileSystem) is string problem)
                throw new InvalidOperationException(problem);
            if (await _uploads.CountOpenAsync(viewer.Id) >= ProjectUploadRules.MaxOpenPerUser)
                throw new InvalidOperationException($"لديك {ProjectUploadRules.MaxOpenPerUser} عمليات رفع مفتوحة — أنهِ إحداها أو ألغِها أولاً");
            if (!_files.HasFreeSpace(dto.Size))
                throw new InvalidOperationException("لا توجد مساحة كافية على الخادم لهذا الملف — أبلغ مدير النظام");

            var now = DateTime.UtcNow;
            var upload = new ProjectUpload
            {
                Id = Guid.NewGuid(), ProjectId = dto.ProjectId, DocumentId = dto.DocumentId, Title = title, Kind = kind,
                FileName = fileName, Size = dto.Size, Source = (dto.Source ?? string.Empty).Trim(), Note = (dto.Note ?? string.Empty).Trim(),
                UserId = viewer.Id, Status = ProjectUploadStatus.Receiving, CreatedAt = now, UpdatedAt = now
            };
            await _uploads.AddAsync(upload);
            return ToDto(upload);
        }

        public async Task<ProjectUploadResponseDto> Handle(WriteProjectChunkCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var upload = await OwnedAsync(viewer, request.Id);
            if (upload.Status != ProjectUploadStatus.Receiving) throw new InvalidOperationException("هذا الرفع لم يعد يقبل أجزاء");

            switch (ProjectUploadRules.DecideChunk(upload.Received, upload.Size, _settings.ChunkBytes, request.Offset, request.Length))
            {
                case ProjectUploadRules.ChunkDecision.BadLength:
                    throw new InvalidOperationException($"طول الجزء غير صحيح (الأقصى {ProjectUploadRules.Describe(_settings.ChunkBytes)} ولا يتجاوز حجم الملف)");
                case ProjectUploadRules.ChunkDecision.OutOfOrder:
                    throw new InvalidOperationException($"الجزء خارج الترتيب: المستلم {upload.Received} بايت — استأنف من هناك");
                case ProjectUploadRules.ChunkDecision.AlreadyReceived:
                    return ToDto(upload);
            }

            var length = await _files.WriteChunkAsync(upload.Id, request.Offset, request.Body, request.Length, ct);
            await _uploads.SetReceivedAsync(upload.Id, length);
            upload.Received = length;
            return ToDto(upload);
        }

        public async Task<ProjectUploadResponseDto> Handle(GetProjectUploadQuery request, CancellationToken ct) =>
            ToDto(await OwnedAsync(await ViewerAsync(), request.Id));

        public async Task<List<ProjectUploadResponseDto>> Handle(ListMyProjectUploadsQuery request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            return (await _uploads.GetMineAsync(viewer.Id, request.ProjectId)).Select(ToDto).ToList();
        }

        public async Task<ProjectUploadResponseDto> Handle(CompleteProjectUploadCommand request, CancellationToken ct)
        {
            var viewer = await ViewerAsync();
            var upload = await OwnedAsync(viewer, request.Id);
            if (ProjectUploadRules.CheckComplete(upload) is string problem) throw new InvalidOperationException(problem);
            await OpenVisibleProjectAsync(viewer, upload.ProjectId);   // أُغلق المشروع أو خرج من حدّه أثناء الرفع؟

            await _uploads.SetStatusAsync(upload.Id, ProjectUploadStatus.Processing);
            _signal.Notify();
            upload.Status = ProjectUploadStatus.Processing;
            return ToDto(upload);
        }

        public async Task<Unit> Handle(CancelProjectUploadCommand request, CancellationToken ct)
        {
            var upload = await OwnedAsync(await ViewerAsync(), request.Id);
            if (upload.Status == ProjectUploadStatus.Processing)
                throw new InvalidOperationException("الملف قيد الفحص والحفظ ولا يمكن إلغاؤه الآن");
            _files.Delete(upload.Id);
            await _uploads.DeleteAsync(upload.Id);
            return Unit.Value;
        }

        private ProjectUploadResponseDto ToDto(ProjectUpload u) => new()
        {
            Id = u.Id, ProjectId = u.ProjectId, DocumentId = u.DocumentId, Title = u.Title, Kind = (int)u.Kind,
            FileName = u.FileName, Size = u.Size, Received = u.Received, ChunkSize = _settings.ChunkBytes,
            Status = (int)u.Status, StatusName = ProjectUploadRules.StatusAr(u.Status), Error = u.Error,
            VersionId = u.VersionId, CreatedAt = u.CreatedAt, UpdatedAt = u.UpdatedAt
        };
    }

    /// <summary>
    /// إنهاء رفع اكتملت أجزاؤه (تستدعيه الخدمة الخلفية): يحسب SHA-256، ويكشف النوع من المحتوى، وينقل الملف إلى مخزن المرفقات
    /// (أو يقرؤه إلى القاعدة في وضع Database)، ثم ينشئ الوثيقة والإصدار وأثر التدقيق في معاملة واحدة.
    /// يعيد سبب الرفض (فيُعلَّم الرفع فاشلاً)، أو null عند النجاح. خطأ غير متوقع يُترك يصعد ليسجّله المستدعي.
    /// </summary>
    public class ProjectUploadFinalizer
    {
        private readonly IProjectUploadService _uploads;
        private readonly IProjectUploadFiles _files;
        private readonly IProjectService _projects;
        private readonly IProjectDocumentService _documents;
        private readonly IProjectFileSettings _settings;

        public ProjectUploadFinalizer(IProjectUploadService uploads, IProjectUploadFiles files, IProjectService projects,
            IProjectDocumentService documents, IProjectFileSettings settings)
        {
            _uploads = uploads;
            _files = files;
            _projects = projects;
            _documents = documents;
            _settings = settings;
        }

        public async Task<string?> FinalizeAsync(Guid id, CancellationToken ct)
        {
            var upload = await _uploads.GetAsync(id);
            if (upload == null || upload.Status != ProjectUploadStatus.Processing) return null;

            var project = await _projects.GetByIdAsync(upload.ProjectId);
            if (project == null) return "المشروع لم يعد موجوداً";
            if (ProjectRules.IsFinished(project.Stage)) return $"المشروع {ProjectRules.StageAr(project.Stage)} أثناء الرفع — أعد فتحه ثم أعد الرفع";
            if (upload.DocumentId is int documentId && (await _documents.GetDocumentAsync(documentId))?.ProjectId != upload.ProjectId)
                return "الوثيقة لم تعد موجودة في هذا المشروع";

            if (_files.LengthOf(id) != upload.Size) return "حجم الملف المحفوظ لا يطابق المعلن — أعد رفعه";

            string sha256;
            MapFileRules.Detected? detected;
            await using (var part = _files.OpenPart(id))
            {
                sha256 = Convert.ToHexString(await SHA256.HashDataAsync(part, ct)).ToLowerInvariant();
                var (problem, found) = ProjectUploadRules.CheckType(upload.FileName, part);
                if (problem != null) return problem;
                detected = found;
            }

            string? key = null;
            byte[]? content = null;
            if (_settings.UsesFileSystem)
                key = _files.Promote(id, FileAreas.Projects);
            else
            {
                await using var part = _files.OpenPart(id);
                content = new byte[part.Length];
                await part.ReadExactlyAsync(content, ct);
            }

            await _documents.AddVersionAsync(id, new NewVersionData(upload.FileName, detected!.ContentType, upload.Size, sha256, key), content);
            _files.Delete(id);   // وضع Database: الملف المؤقت لم يعد لازماً (وضع المجلد: نُقل)
            return null;
        }
    }
}
