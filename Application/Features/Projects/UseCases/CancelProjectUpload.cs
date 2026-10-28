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
    public record CancelProjectUploadCommand(Guid Id) : IRequest<Unit>;


    public class CancelProjectUploadHandler(ProjectUploadContext ctx) : IRequestHandler<CancelProjectUploadCommand, Unit>
    {
        public async Task<Unit> Handle(CancelProjectUploadCommand request, CancellationToken ct)
        {
            var upload = await ctx.OwnedAsync(await ctx.ViewerAsync(), request.Id);
            if (upload.Status == ProjectUploadStatus.Processing)
                throw new InvalidOperationException("الملف قيد الفحص والحفظ ولا يمكن إلغاؤه الآن");
            ctx.Files.Delete(upload.Id);
            await ctx.Uploads.DeleteAsync(upload.Id);
            return Unit.Value;
        }
    }
}
