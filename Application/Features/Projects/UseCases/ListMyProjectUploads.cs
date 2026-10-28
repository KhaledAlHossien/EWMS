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
    public record ListMyProjectUploadsQuery(int? ProjectId) : IRequest<List<ProjectUploadResponseDto>>;


    public class ListMyProjectUploadsHandler(ProjectUploadContext ctx) : IRequestHandler<ListMyProjectUploadsQuery, List<ProjectUploadResponseDto>>
    {
        public async Task<List<ProjectUploadResponseDto>> Handle(ListMyProjectUploadsQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            return (await ctx.Uploads.GetMineAsync(viewer.Id, request.ProjectId)).Select(ctx.ToDto).ToList();
        }
    }
}
