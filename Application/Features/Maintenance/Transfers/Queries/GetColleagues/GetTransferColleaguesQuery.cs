using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Transfers.Queries.GetColleagues
{
    /// <summary>زملاء قسمي (لاقتراح من يُحوَّل إليه الطلب)</summary>
    public record GetTransferColleaguesQuery : IRequest<List<TechnicianOptionDto>>;
}
