using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Queries.ApprovalContext
{
    public record GetVacationApprovalContextQuery(int Id) : IRequest<VacationApprovalContextDto>;
}
