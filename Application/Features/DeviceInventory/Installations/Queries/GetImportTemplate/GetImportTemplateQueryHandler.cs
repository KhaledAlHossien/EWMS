using Application.Interfaces;
using MediatR;

namespace Application.Features.DeviceInventory.Installations.Queries.GetImportTemplate
{
    public class GetImportTemplateQueryHandler : IRequestHandler<GetImportTemplateQuery, byte[]>
    {
        private readonly IDeviceSpreadsheet _spreadsheet;

        public GetImportTemplateQueryHandler(IDeviceSpreadsheet spreadsheet)
        {
            _spreadsheet = spreadsheet;
        }

        public Task<byte[]> Handle(GetImportTemplateQuery request, CancellationToken ct) => Task.FromResult(_spreadsheet.ImportTemplate());
    }
}
