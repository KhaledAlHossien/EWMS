using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Reports.Queries.GetLogOptions
{
    public class GetReportLogOptionsQueryHandler : IRequestHandler<GetReportLogOptionsQuery, List<ReportOptionDto>>
    {
        private readonly IReadOnlyList<IReport> _reports;

        public GetReportLogOptionsQueryHandler(IEnumerable<IReport> reports)
        {
            _reports = reports.ToList();
        }

        public Task<List<ReportOptionDto>> Handle(GetReportLogOptionsQuery request, CancellationToken ct) =>
            Task.FromResult(_reports.Select(r => new ReportOptionDto(r.Key, r.Title, r.Group))
                .Append(new ReportOptionDto(ReportRules.TasksKey, ReportRules.TasksTitle, "المهام")).ToList());
    }
}
