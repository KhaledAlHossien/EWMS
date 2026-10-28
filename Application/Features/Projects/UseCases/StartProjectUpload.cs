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
    public record StartProjectUploadCommand(StartProjectUploadRequestDto Dto) : IRequest<ProjectUploadResponseDto>;

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

    public class StartProjectUploadHandler(ProjectUploadContext ctx) : IRequestHandler<StartProjectUploadCommand, ProjectUploadResponseDto>
    {
        public async Task<ProjectUploadResponseDto> Handle(StartProjectUploadCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var dto = request.Dto;
            await ctx.OpenVisibleProjectAsync(viewer, dto.ProjectId);

            var title = (dto.Title ?? string.Empty).Trim();
            var kind = dto.Kind is int k ? (ProjectDocumentKind)k : ProjectDocumentKind.Other;
            if (dto.DocumentId is int documentId)
            {
                var document = await ctx.Documents.GetDocumentAsync(documentId);
                if (document == null || document.ProjectId != dto.ProjectId) throw new KeyNotFoundException("الوثيقة غير موجودة في هذا المشروع");
                title = document.Title;
                kind = document.Kind;
            }
            else if (title.Length == 0)
                throw new InvalidOperationException("عنوان الوثيقة مطلوب");

            var fileName = MapFileRules.SafeFileName(dto.FileName);
            if (ProjectUploadRules.ValidateStart(fileName, dto.Size, ctx.Settings.EffectiveMaxBytes, ctx.Settings.UsesFileSystem) is string problem)
                throw new InvalidOperationException(problem);
            if (await ctx.Uploads.CountOpenAsync(viewer.Id) >= ProjectUploadRules.MaxOpenPerUser)
                throw new InvalidOperationException($"لديك {ProjectUploadRules.MaxOpenPerUser} عمليات رفع مفتوحة — أنهِ إحداها أو ألغِها أولاً");
            if (!ctx.Files.HasFreeSpace(dto.Size))
                throw new InvalidOperationException("لا توجد مساحة كافية على الخادم لهذا الملف — أبلغ مدير النظام");

            var now = DateTime.UtcNow;
            var upload = new ProjectUpload
            {
                Id = Guid.NewGuid(), ProjectId = dto.ProjectId, DocumentId = dto.DocumentId, Title = title, Kind = kind,
                FileName = fileName, Size = dto.Size, Source = (dto.Source ?? string.Empty).Trim(), Note = (dto.Note ?? string.Empty).Trim(),
                UserId = viewer.Id, Status = ProjectUploadStatus.Receiving, CreatedAt = now, UpdatedAt = now
            };
            await ctx.Uploads.AddAsync(upload);
            return ctx.ToDto(upload);
        }
    }
}
