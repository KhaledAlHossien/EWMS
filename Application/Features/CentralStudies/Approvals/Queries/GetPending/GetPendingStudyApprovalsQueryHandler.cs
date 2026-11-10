using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;
using MediatR;

namespace Application.Features.CentralStudies.Approvals.Queries.GetPending
{
    public class GetPendingStudyApprovalsQueryHandler : IRequestHandler<GetPendingStudyApprovalsQuery, List<StudyApprovalQueueItemDto>>
    {
        private readonly IStudyApprovalService _service;
        private readonly StudyKit _kit;

        public GetPendingStudyApprovalsQueryHandler(IStudyApprovalService service, StudyKit kit)
        {
            _service = service;
            _kit = kit;
        }

        /// <summary>«بانتظار توقيعي»: السطور التي يستطيع المستخدم التوقيع عليها الآن</summary>
        public async Task<List<StudyApprovalQueueItemDto>> Handle(GetPendingStudyApprovalsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var roles = Enum.GetValues<StudySignerRole>().Where(r => viewer.Has(StudyRules.SignPermission(r))).ToList();
            if (roles.Count == 0) return [];

            var rows = await _service.GetPendingAsync(roles);
            var result = new List<StudyApprovalQueueItemDto>();
            foreach (var group in rows.GroupBy(r => ApprovalOwner.Of(r)))
            {
                var owner = group.Key;
                if (!owner.Pending || !StudyRules.InScope(viewer, owner.BranchId)) continue;
                var siblings = await _service.GetForOwnerAsync(owner.Type, owner.Id);
                foreach (var row in group)
                {
                    var current = siblings.First(s => s.Id == row.Id);
                    if (!StudyKit.CanDecide(viewer, current, siblings, owner)) continue;
                    var (title, branchName) = Describe(row);
                    result.Add(new StudyApprovalQueueItemDto
                    {
                        ApprovalId = row.Id,
                        OwnerType = (int)owner.Type,
                        OwnerTypeName = StudyRules.Name(owner.Type),
                        OwnerId = owner.Id,
                        OwnerNumber = owner.Number,
                        OwnerTitle = title,
                        SignerRole = (int)row.SignerRole,
                        SignerRoleName = StudyRules.Name(row.SignerRole),
                        SortOrder = row.SortOrder,
                        BranchId = owner.BranchId,
                        BranchName = branchName
                    });
                }
            }
            return result;
        }

        private static (string Title, string BranchName) Describe(StudyApproval row) =>
            row.Assignment != null ? (row.Assignment.Request.MainBeneficiary, row.Assignment.Request.Branch.Name)
            : row.Response != null ? (row.Response.Assignment.Request.MainBeneficiary, row.Response.Assignment.Request.Branch.Name)
            : row.StartOrder != null ? (row.StartOrder.Response.Assignment.Request.MainBeneficiary, row.StartOrder.Response.Assignment.Request.Branch.Name)
            : row.DesignApproval != null ? ($"{row.DesignApproval.Version.Drawing.Name} — {StudyRules.VersionLabel(row.DesignApproval.Version.VersionNumber)}",
                row.DesignApproval.Version.Drawing.Project.Branch.Name)
            : row.Project != null ? (row.Project.Name, row.Project.Branch.Name)
            : (string.Empty, string.Empty);
    }
}
