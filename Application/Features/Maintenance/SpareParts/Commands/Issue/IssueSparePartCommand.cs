using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.SpareParts.Commands.Issue
{
    public record IssueSparePartCommand(int RequestId, IssueSparePartDto Dto) : IRequest<RequestPartsDto>;
}
