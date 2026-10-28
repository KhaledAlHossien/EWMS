using Application.Common;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Files;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories
{
    /// <summary>الأجزاء المؤقتة على القرص: <c>{id}.part</c> لكل جلسة رفع، بمجلد مستقل عن مفاتيح الملفات النهائية فلا يمسّها المنظّف</summary>
    public class ProjectUploadFiles : IProjectUploadFiles
    {
        private readonly ProjectFileSettings _settings;
        private readonly FileStore _store;

        public ProjectUploadFiles(ProjectFileSettings settings, IFileStore store)
        {
            _settings = settings;
            _store = (FileStore)store;
        }

        private string PartPath(Guid id) => Path.Combine(_settings.TempFolder, id.ToString("N") + ".part");

        public long LengthOf(Guid uploadId)
        {
            var info = new FileInfo(PartPath(uploadId));
            return info.Exists ? info.Length : 0;
        }

        public async Task<long> WriteChunkAsync(Guid uploadId, long offset, Stream body, long length, CancellationToken ct)
        {
            Directory.CreateDirectory(_settings.TempFolder);
            try
            {
                await using var file = new FileStream(PartPath(uploadId), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None,
                    bufferSize: 81920, FileOptions.Asynchronous);
                if (file.Length < offset)
                    throw new InvalidOperationException("الملف المؤقت أقصر مما سُجّل استلامه — أعد بدء الرفع");

                // الجزء يُكتب فوق ما قبله من محاولة انقطعت: يُقطع الملف عند بدايته أولاً
                file.SetLength(offset);
                file.Position = offset;

                var buffer = new byte[81920];
                var remaining = length;
                while (remaining > 0)
                {
                    var read = await body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct);
                    if (read == 0)
                    {
                        file.SetLength(offset);   // جزء ناقص لا يبقى
                        throw new InvalidOperationException("انقطع الاتصال قبل اكتمال الجزء — أعد إرساله");
                    }
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    remaining -= read;
                }
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
                return offset + length;
            }
            catch (IOException ex) when (ex is not EndOfStreamException && IsSharingViolation(ex))
            {
                throw new InvalidOperationException("يوجد إرسال آخر لهذا الملف قيد التنفيذ — انتظر انتهاءه", ex);
            }
        }

        // خطأ مشاركة الملف (رفعان متزامنان للجلسة نفسها)، بخلاف فشل الكتابة الفعلي الذي يُترك ليُبلَّغ بخطأ الخادم
        private static bool IsSharingViolation(IOException ex) => (ex.HResult & 0xFFFF) is 32 or 33;

        public Stream OpenPart(Guid uploadId) =>
            new FileStream(PartPath(uploadId), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

        public bool HasFreeSpace(long size)
        {
            try
            {
                Directory.CreateDirectory(_settings.TempFolder);
                var root = Path.GetPathRoot(Path.GetFullPath(_settings.TempFolder));
                // مشاركة SMB قد لا تُبلغ عن مساحتها: يُسمح بالرفع ويفشل بخطأ كتابة واضح إن امتلأت
                return root == null || new DriveInfo(root).AvailableFreeSpace > size;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        public string Promote(Guid uploadId, string area) => _store.Adopt(area, PartPath(uploadId));

        public void Delete(Guid uploadId) => FileStore.TryDelete(PartPath(uploadId));
    }

    public class ProjectUploadService : IProjectUploadService
    {
        private readonly DataContext _context;

        public ProjectUploadService(DataContext context) => _context = context;

        public Task<ProjectUpload?> GetAsync(Guid id) =>
            _context.ProjectUploads.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

        public Task<List<ProjectUpload>> GetMineAsync(int userId, int? projectId) =>
            _context.ProjectUploads.AsNoTracking()
                .Where(u => u.UserId == userId && (projectId == null || u.ProjectId == projectId))
                .OrderByDescending(u => u.CreatedAt).Take(50).ToListAsync();

        public Task<int> CountOpenAsync(int userId) =>
            _context.ProjectUploads.CountAsync(u => u.UserId == userId
                && (u.Status == ProjectUploadStatus.Receiving || u.Status == ProjectUploadStatus.Processing));

        public async Task AddAsync(ProjectUpload upload)
        {
            _context.ProjectUploads.Add(upload);
            await _context.SaveChangesAsync();
        }

        public Task SetReceivedAsync(Guid id, long received) =>
            _context.ProjectUploads.Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Received, received).SetProperty(u => u.UpdatedAt, DateTime.UtcNow));

        public Task SetStatusAsync(Guid id, ProjectUploadStatus status, string error = "") =>
            _context.ProjectUploads.Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, status)
                    .SetProperty(u => u.Error, error.Length > 500 ? error[..499] + "…" : error)
                    .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));

        public Task DeleteAsync(Guid id) => _context.ProjectUploads.Where(u => u.Id == id).ExecuteDeleteAsync();

        public Task<List<Guid>> GetProcessingIdsAsync() =>
            _context.ProjectUploads.AsNoTracking().Where(u => u.Status == ProjectUploadStatus.Processing)
                .OrderBy(u => u.UpdatedAt).Select(u => u.Id).ToListAsync();

        public Task<List<Guid>> GetExpiredIdsAsync(DateTime receivingBefore, DateTime finishedBefore) =>
            _context.ProjectUploads.AsNoTracking()
                .Where(u => (u.Status == ProjectUploadStatus.Receiving && u.UpdatedAt < receivingBefore)
                            || ((u.Status == ProjectUploadStatus.Done || u.Status == ProjectUploadStatus.Failed) && u.UpdatedAt < finishedBefore))
                .Select(u => u.Id).ToListAsync();

        public Task<int> FailStuckAsync(DateTime before) =>
            _context.ProjectUploads.Where(u => u.Status == ProjectUploadStatus.Processing && u.UpdatedAt < before)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, ProjectUploadStatus.Failed)
                    .SetProperty(u => u.Error, "توقف إنهاء الملف قبل اكتماله — أعد رفعه")
                    .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
    }

    public class ProjectDocumentService : IProjectDocumentService
    {
        private const string VersionNumberIndex = "IX_ProjectDocumentVersions_DocumentId_VersionNumber";
        private readonly DataContext _context;

        public ProjectDocumentService(DataContext context) => _context = context;

        public Task<ProjectDocument?> GetDocumentAsync(int id) =>
            _context.ProjectDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

        public async Task<ProjectDocumentVersion> AddVersionAsync(Guid uploadId, NewVersionData data, byte[]? content)
        {
            // رفعان متزامنان لوثيقة واحدة قد يحجزان الرقم نفسه: الفهرس الفريد يمنع الثاني فيُعاد بالرقم التالي
            for (var attempt = 1; ; attempt++)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var upload = await _context.ProjectUploads.FirstAsync(u => u.Id == uploadId);
                    var project = await _context.Projects.AsNoTracking().FirstAsync(p => p.Id == upload.ProjectId);
                    var now = DateTime.UtcNow;

                    ProjectDocument document;
                    var isNew = upload.DocumentId is null;
                    if (upload.DocumentId is int documentId)
                        document = await _context.ProjectDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.ProjectId == upload.ProjectId)
                            ?? throw new InvalidOperationException("الوثيقة المطلوب إضافة إصدار لها لم تعد موجودة");
                    else
                    {
                        document = new ProjectDocument
                        {
                            ProjectId = upload.ProjectId, Title = upload.Title, Kind = upload.Kind,
                            CreatedByUserId = upload.UserId, CreatedAt = now
                        };
                        _context.ProjectDocuments.Add(document);
                    }

                    var last = isNew ? 0 : await _context.ProjectDocumentVersions
                        .Where(v => v.DocumentId == document.Id).MaxAsync(v => (int?)v.VersionNumber) ?? 0;
                    var version = new ProjectDocumentVersion
                    {
                        Document = document,
                        VersionNumber = last + 1,
                        FileName = data.FileName, ContentType = data.ContentType, Size = data.Size, Sha256 = data.Sha256,
                        StorageKey = data.StorageKey, Source = upload.Source, Note = upload.Note,
                        UploadedByUserId = upload.UserId, UploadedAt = now,
                        Content = content == null ? null : new ProjectDocumentVersionContent { Data = content }
                    };
                    _context.ProjectDocumentVersions.Add(version);
                    await _context.SaveChangesAsync();

                    _context.AuditLogs.Add(new AuditLog
                    {
                        CreatedAt = now, UserId = upload.UserId, Action = AuditActions.Uploaded, EntityType = AuditEntities.Version,
                        EntityId = version.Id, ProjectId = project.Id, BranchId = project.BranchId,
                        Title = version.VersionNumber == 1 && isNew
                            ? $"إضافة وثيقة «{document.Title}» (الإصدار 1)"
                            : $"رفع الإصدار {version.VersionNumber} من «{document.Title}»",
                        Details = $"الملف: {data.FileName}\nالحجم: {ProjectUploadRules.Describe(data.Size)}\nSHA-256: {data.Sha256}"
                    });
                    upload.Status = ProjectUploadStatus.Done;
                    upload.VersionId = version.Id;
                    upload.Error = string.Empty;
                    upload.UpdatedAt = now;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return version;
                }
                catch (DbUpdateException ex) when (attempt < 3 && ex.ToString().Contains(VersionNumberIndex))
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                }
            }
        }

        public async Task<(List<ProjectDocument> Items, int TotalCount)> SearchAsync(
            System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, DocumentFilter filter, int page, int pageSize)
        {
            var query = Filtered(_context, visibleProjects, filter);
            var total = await query.CountAsync();
            return (await PageOf(query, page, pageSize).ToListAsync(), total);
        }

        /// <summary>الوثائق المرئية بالشرط والفلاتر (بلا ترتيب أو تضمين) — public ليُفحص ترجمتها إلى SQL في الاختبارات</summary>
        public static IQueryable<ProjectDocument> Filtered(DataContext context,
            System.Linq.Expressions.Expression<Func<Project, bool>> visibleProjects, DocumentFilter filter)
        {
            var visibleIds = context.Projects.Where(visibleProjects).Select(p => p.Id);
            var query = context.ProjectDocuments.AsNoTracking().Where(d => visibleIds.Contains(d.ProjectId));

            if (filter.ProjectId is int projectId) query = query.Where(d => d.ProjectId == projectId);
            if (filter.Kind is ProjectDocumentKind kind) query = query.Where(d => d.Kind == kind);
            if (filter.From is DateTime from) query = query.Where(d => d.Versions.Any(v => v.UploadedAt >= from));
            if (filter.To is DateTime to) query = query.Where(d => d.Versions.Any(v => v.UploadedAt < to.AddDays(1)));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var text = filter.Search.Trim();
                // «PRJ-2026-00042»: الأرقام الأخيرة = رقم المشروع
                var digits = new string(text.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
                var id = text.StartsWith("PRJ", StringComparison.OrdinalIgnoreCase) && int.TryParse(digits, out var n) ? n : -1;
                query = query.Where(d => d.Title.Contains(text) || d.Project.Name.Contains(text) || d.ProjectId == id
                                         || d.Versions.Any(v => v.FileName.Contains(text)));
            }

            return query;
        }

        public static IQueryable<ProjectDocument> PageOf(IQueryable<ProjectDocument> filtered, int page, int pageSize) => filtered
            .Include(d => d.Project).Include(d => d.CreatedByUser)
            .Include(d => d.Versions).ThenInclude(v => v.UploadedByUser)
            .Include(d => d.Versions).ThenInclude(v => v.ApprovedByUser)
            .OrderByDescending(d => d.Versions.Max(v => (DateTime?)v.UploadedAt) ?? d.CreatedAt).ThenByDescending(d => d.Id)
            .AsSplitQuery()
            .Skip((page - 1) * pageSize).Take(pageSize);

        public Task<ProjectDocument?> GetWithVersionsAsync(int id) =>
            _context.ProjectDocuments.AsNoTracking().AsSplitQuery()
                .Include(d => d.CreatedByUser)
                .Include(d => d.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
                .Include(d => d.Versions).ThenInclude(v => v.UploadedByUser)
                .Include(d => d.Versions).ThenInclude(v => v.ApprovedByUser)
                .FirstOrDefaultAsync(d => d.Id == id);

        public Task<ProjectDocumentVersion?> GetVersionAsync(int versionId) =>
            _context.ProjectDocumentVersions.AsNoTracking().AsSplitQuery()
                .Include(v => v.UploadedByUser).Include(v => v.ApprovedByUser)
                .Include(v => v.Document).ThenInclude(d => d.Project).ThenInclude(p => p.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(v => v.Id == versionId);

        public Task<byte[]?> GetContentAsync(int versionId) =>
            _context.ProjectDocumentVersionContents.AsNoTracking()
                .Where(c => c.VersionId == versionId).Select(c => (byte[]?)c.Data).FirstOrDefaultAsync();

        public async Task ApproveAsync(int versionId, int userId, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = DateTime.UtcNow;

            // الشرط في الاستعلام نفسه: اعتماد شخصين للإصدار في اللحظة نفسها لا ينجح إلا لأحدهما
            var changed = await _context.ProjectDocumentVersions.Where(v => v.Id == versionId && !v.IsApproved)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsApproved, true)
                    .SetProperty(v => v.ApprovedByUserId, userId).SetProperty(v => v.ApprovedAt, now));
            if (changed == 0) throw new InvalidOperationException("هذا الإصدار معتمد مسبقاً");

            var info = await _context.ProjectDocumentVersions.AsNoTracking().Where(v => v.Id == versionId)
                .Select(v => new { v.Document.ProjectId, v.Document.Project.BranchId }).FirstAsync();
            _context.AuditLogs.Add(NewLog(audit with { EntityId = versionId }, info.ProjectId, info.BranchId, now));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task DeleteDocumentAsync(int documentId, AuditEntry audit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var document = await _context.ProjectDocuments.Include(d => d.Versions).Include(d => d.Project)
                .FirstOrDefaultAsync(d => d.Id == documentId) ?? throw new KeyNotFoundException("الوثيقة غير موجودة");
            // اعتُمد إصدار منها بعد فحص المعالج؟ لا تُحذف
            ProjectDocumentRules.EnsureCanDelete(document);
            if (await _context.StudyDeliverables.AnyAsync(d => d.DocumentId == documentId) || await _context.StudySubmissions.AnyAsync(x => x.DecisionVersion!.DocumentId == documentId))
                throw new InvalidOperationException("هذه الوثيقة ملف مخرج في أمر دراسة: لا تُحذف — أضف إصداراً جديداً بدلاً من ذلك");

            _context.ProjectDocumentVersions.RemoveRange(document.Versions);   // المحتوى (وضع Database) يُحذف بالتسلسل
            _context.ProjectDocuments.Remove(document);
            _context.AuditLogs.Add(NewLog(audit with { EntityId = documentId }, document.ProjectId, document.Project.BranchId, DateTime.UtcNow));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private static AuditLog NewLog(AuditEntry entry, int projectId, int branchId, DateTime now) => new()
        {
            CreatedAt = now, UserId = entry.UserId, Action = entry.Action, EntityType = entry.EntityType, EntityId = entry.EntityId,
            ProjectId = projectId, BranchId = branchId, Title = entry.Title, Details = entry.Details
        };
    }

    /// <summary>إشارة بين طلب «إنهاء الرفع» والخدمة الخلفية (نسخة واحدة لكل التطبيق)</summary>
    public sealed class ProjectUploadSignal : IProjectUploadSignal
    {
        private readonly SemaphoreSlim _semaphore = new(0, int.MaxValue);

        public void Notify() => _semaphore.Release();

        /// <summary>ينتظر إشارة أو انقضاء المهلة (ما ينتهي أولاً)</summary>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
    }
}
