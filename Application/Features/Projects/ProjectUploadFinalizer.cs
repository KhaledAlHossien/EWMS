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
