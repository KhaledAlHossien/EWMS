using Application.DTOs.Response;
using Application.Features.CentralStudies.Stages;
using Application.Interfaces;
using MediatR;

namespace Application.Features.CentralStudies.Tasks.Queries.GetById
{
    public class GetStudyTaskQueryHandler : IRequestHandler<GetStudyTaskQuery, StageTaskDto>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public GetStudyTaskQueryHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        /// <summary>يراها من يرى مشاريع فرعه، أو المكلَّف بها</summary>
        public async Task<StageTaskDto> Handle(GetStudyTaskQuery request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var task = await _service.GetTaskAsync(request.Id) ?? throw new KeyNotFoundException("المهمة غير موجودة");
            var canSee = (viewer.Has(StudyPermissions.ViewProjects) && StudyRules.InScope(viewer, task.Stage.Project.BranchId))
                         || (viewer.Has(StudyPermissions.UpdateTaskProgress) && StudyStageSupport.IsTarget(viewer, task));
            if (!canSee) throw new UnauthorizedAccessException("لا تملك صلاحية عرض هذه المهمة");
            return _support.TaskDto(task, viewer);
        }
    }
}
