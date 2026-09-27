using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Query.GetById
{
    public class GetVacationByIdQuery : IRequest<VacationResponseDto>
    {
        public int Id { get; set; }
        public GetVacationByIdQuery(int id) => Id = id;
    }
}
