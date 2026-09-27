using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.VacationTypes.Commands.Create
{
    public class CreateVacationTypeCommand : IRequest<VacationTypeResponseDto>
    {
        public CreateVacationTypeRequestDto VacationTypeDto { get; set; }

        public CreateVacationTypeCommand(CreateVacationTypeRequestDto dto)
        {
            VacationTypeDto = dto;
        }
    }
}