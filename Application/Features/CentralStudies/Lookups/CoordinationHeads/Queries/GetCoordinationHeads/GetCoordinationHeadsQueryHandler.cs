using Application.DTOs.Response;
using MediatR;

namespace Application.Features.CentralStudies.Lookups.CoordinationHeads.Queries.GetCoordinationHeads
{
    public class GetCoordinationHeadsQueryHandler : IRequestHandler<GetCoordinationHeadsQuery, List<StudyPersonDto>>
    {
        private readonly StudyKit _kit;

        public GetCoordinationHeadsQueryHandler(StudyKit kit) => _kit = kit;

        public async Task<List<StudyPersonDto>> Handle(GetCoordinationHeadsQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var branchId = request.BranchId ?? StudyRules.BranchLimit(viewer) ?? throw new InvalidOperationException("حدد الفرع");
            if (branchId == 0) return [];   // مستخدم بلا فرع وبلا نظرة مؤسسة لا يرى أحداً
            StudyRules.EnsureInScope(viewer, branchId);
            return (await _kit.CoordinationHeadsAsync(branchId)).Select(u => new StudyPersonDto
            {
                Id = u.Id, FullName = u.FullName, DepartmentName = u.Department?.Name, JobTitle = u.Role?.Name
            }).ToList();
        }
    }
}
