using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.Print
{
    public record GetVacationPrintQuery(int Id) : IRequest<VacationPrintDto>;
}
