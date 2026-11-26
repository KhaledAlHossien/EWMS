using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.GetImportTemplate
{
    public record GetImportTemplateQuery : IRequest<byte[]>;
}
