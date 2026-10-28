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
    public record WriteProjectChunkCommand(Guid Id, long Offset, long Length, Stream Body) : IRequest<ProjectUploadResponseDto>;


    public class WriteProjectChunkHandler(ProjectUploadContext ctx) : IRequestHandler<WriteProjectChunkCommand, ProjectUploadResponseDto>
    {
        public async Task<ProjectUploadResponseDto> Handle(WriteProjectChunkCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var upload = await ctx.OwnedAsync(viewer, request.Id);
            if (upload.Status != ProjectUploadStatus.Receiving) throw new InvalidOperationException("هذا الرفع لم يعد يقبل أجزاء");

            switch (ProjectUploadRules.DecideChunk(upload.Received, upload.Size, ctx.Settings.ChunkBytes, request.Offset, request.Length))
            {
                case ProjectUploadRules.ChunkDecision.BadLength:
                    throw new InvalidOperationException($"طول الجزء غير صحيح (الأقصى {ProjectUploadRules.Describe(ctx.Settings.ChunkBytes)} ولا يتجاوز حجم الملف)");
                case ProjectUploadRules.ChunkDecision.OutOfOrder:
                    throw new InvalidOperationException($"الجزء خارج الترتيب: المستلم {upload.Received} بايت — استأنف من هناك");
                case ProjectUploadRules.ChunkDecision.AlreadyReceived:
                    return ctx.ToDto(upload);
            }

            var length = await ctx.Files.WriteChunkAsync(upload.Id, request.Offset, request.Body, request.Length, ct);
            await ctx.Uploads.SetReceivedAsync(upload.Id, length);
            upload.Received = length;
            return ctx.ToDto(upload);
        }
    }
}
