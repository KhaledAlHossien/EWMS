using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Maintenance.Requests.Queries.FindClient
{
    /// <summary>عميل موظف بمطابقة تامة للاسم الكامل أو الرقم الذاتي (MaintenanceClients) — فارغ = عميل خارجي</summary>
    public record FindMaintenanceClientQuery(string Query) : IRequest<List<MaintenanceClientDto>>;
}
