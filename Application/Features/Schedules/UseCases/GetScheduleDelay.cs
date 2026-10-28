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
    public record GetScheduleDelayQuery(int Id) : IRequest<ScheduleDelayResponseDto>;


    public class GetScheduleDelayHandler(ScheduleChangeContext ctx) : IRequestHandler<GetScheduleDelayQuery, ScheduleDelayResponseDto>
    {
        public async Task<ScheduleDelayResponseDto> Handle(GetScheduleDelayQuery request, CancellationToken ct)
        {
            var viewer = await ctx.ViewerAsync();
            return await ctx.DelayDetailAsync(viewer, await ctx.LoadDelayAsync(viewer, request.Id));
        }
    }
}
