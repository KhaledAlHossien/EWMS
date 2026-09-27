using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Offices.Commands.Create
{
    public record CreateOfficeCommand(CreateOfficeRequestDto OfficeDto)
        : IRequest<OfficeResponseDto>;
}
