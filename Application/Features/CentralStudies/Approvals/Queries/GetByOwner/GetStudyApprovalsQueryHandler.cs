using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Queries.GetByOwner
{
    public class GetStudyApprovalsQueryHandler : IRequestHandler<GetStudyApprovalsQuery, List<StudyApprovalDto>>
    {
        private readonly IStudyApprovalService _service;
        private readonly IStudyProjectService _projects;
        private readonly StudyKit _kit;

        public GetStudyApprovalsQueryHandler(IStudyApprovalService service, IStudyProjectService projects, StudyKit kit)
        {
            _service = service;
            _projects = projects;
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
            var viewer = await _kit.ViewerAsync();
            StudyRules.EnsureInScope(viewer, owner.BranchId);
            var approvals = await _kit.ApprovalsAsync(owner);
            // تواقيع إنجاز المشروع (2026-11-10): للرؤية الكاملة أو من وقّع سطراً أو يملك صلاحية توقيع سطر منها؛ وإلا 403
            if (type == StudyEntityType.Project)
            {
                var project = await _projects.GetByIdAsync(owner.Id) ?? throw new KeyNotFoundException("المشروع غير موجود");
                if (!ProjectScope.For(viewer, project).CanSeeApprovals(viewer, approvals))
                    throw new UnauthorizedAccessException("لا تملك صلاحية عرض تواقيع هذا المشروع");
            }
            return approvals;
        }
    }
}
