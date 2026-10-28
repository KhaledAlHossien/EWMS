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
    public record CompleteProjectUploadCommand(Guid Id) : IRequest<ProjectUploadResponseDto>;


    public class CompleteProjectUploadHandler(ProjectUploadContext ctx) : IRequestHandler<CompleteProjectUploadCommand, ProjectUploadResponseDto>
    {
        public async Task<ProjectUploadResponseDto> Handle(CompleteProjectUploadCommand request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            var upload = await ctx.OwnedAsync(viewer, request.Id);
            if (ProjectUploadRules.CheckComplete(upload) is string problem) throw new InvalidOperationException(problem);
            await ctx.OpenVisibleProjectAsync(viewer, upload.ProjectId);   // أُغلق المشروع أو خرج من حدّه أثناء الرفع؟

            await ctx.Uploads.SetStatusAsync(upload.Id, ProjectUploadStatus.Processing);
            ctx.Signal.Notify();
            upload.Status = ProjectUploadStatus.Processing;
            return ctx.ToDto(upload);
        }
    }
}
