using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Offices.Commands.Update
{
    public record UpdateOfficeCommand(int Id, UpdateOfficeRequestDto OfficeDto)
        : IRequest<OfficeResponseDto>;
}
