using Application.DTOs.Response;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.Vacations.Query.GetPendingForMe
{
    public class GetPendingVacationsForMeQuery
        : IRequest<List<VacationResponseDto>>
    { }
}
