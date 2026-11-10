using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Queries.GetByOwner
{
    public class GetStudyApprovalsQueryHandler : IRequestHandler<GetStudyApprovalsQuery, List<StudyApprovalDto>>
    {
        private readonly IStudyApprovalService _service;
        private readonly StudyKit _kit;

        public GetStudyApprovalsQueryHandler(IStudyApprovalService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        /// <summary>سطور مستند (لمن يرى أي مستند في فرعه)</summary>
        public async Task<List<StudyApprovalDto>> Handle(GetStudyApprovalsQuery request, CancellationToken ct)
        {
            var type = (StudyEntityType)request.OwnerType;
            if (StudyRules.RequiredSigners(type).Count == 0) throw new ArgumentException("هذا النوع لا يُعتمد بالتواقيع");
            var rows = await _service.GetForOwnerAsync(type, request.OwnerId);
            if (rows.Count == 0) return [];
            var row = await _service.GetWithOwnerAsync(rows[0].Id) ?? throw new KeyNotFoundException("المستند غير موجود");
            var owner = ApprovalOwner.Of(row);
            StudyRules.EnsureInScope(await _kit.ViewerAsync(), owner.BranchId);
            return await _kit.ApprovalsAsync(owner);
        }
    }
}
