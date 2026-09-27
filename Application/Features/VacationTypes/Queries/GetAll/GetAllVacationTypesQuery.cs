using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.VacationTypes.Queries.GetAll
{
    public class GetAllVacationTypesQuery : IRequest<List<VacationTypeResponseDto>> { }
}
