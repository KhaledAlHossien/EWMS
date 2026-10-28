using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleChangeSupport;

namespace Application.Features.Schedules
{
    public record GetScheduleExtensionQuery(int Id) : IRequest<ScheduleExtensionResponseDto>;


    public class GetScheduleExtensionHandler(ScheduleChangeContext ctx) : IRequestHandler<GetScheduleExtensionQuery, ScheduleExtensionResponseDto>
    {
        public async Task<ScheduleExtensionResponseDto> Handle(GetScheduleExtensionQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            return await ctx.ExtensionDetailAsync(viewer, await ctx.LoadExtensionAsync(viewer, request.Id));
        }
    }
}
