using Application.DTOs.Response;
using MediatR;

namespace Application.Features.DeviceSites.Queries.GetById
{
    public record GetDeviceSiteByIdQuery(int Id) : IRequest<DeviceSiteResponseDto>;
}
