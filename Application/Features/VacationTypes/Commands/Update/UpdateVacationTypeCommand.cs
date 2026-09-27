using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.VacationTypes.Commands.Update
{
    public class UpdateVacationTypeCommand : IRequest<VacationTypeResponseDto>
    {
        public int Id { get; set; }
        public UpdateVacationTypeRequestDto VacationTypeDto { get; set; }

        public UpdateVacationTypeCommand(int id, UpdateVacationTypeRequestDto dto)
        {
            Id = id;
            VacationTypeDto = dto;
        }
    }
}