using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.Schedules.ScheduleSupport;

namespace Application.Features.Schedules
{
    public record GetScheduleQuery(int Id) : IRequest<ScheduleDetailResponseDto>;


    public class GetScheduleHandler(ScheduleContext ctx) : IRequestHandler<GetScheduleQuery, ScheduleDetailResponseDto>
    {
        public async Task<ScheduleDetailResponseDto> Handle(GetScheduleQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            return await ctx.DetailAsync(viewer, await ctx.LoadScheduleAsync(viewer, request.Id));
        }
    }
}
