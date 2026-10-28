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
    public record GetProjectUploadQuery(Guid Id) : IRequest<ProjectUploadResponseDto>;


    public class GetProjectUploadHandler(ProjectUploadContext ctx) : IRequestHandler<GetProjectUploadQuery, ProjectUploadResponseDto>
    {
        public async Task<ProjectUploadResponseDto> Handle(GetProjectUploadQuery request, CancellationToken ct) =>
            ctx.ToDto(await ctx.OwnedAsync(await ctx.ViewerAsync(), request.Id));
    }
}
