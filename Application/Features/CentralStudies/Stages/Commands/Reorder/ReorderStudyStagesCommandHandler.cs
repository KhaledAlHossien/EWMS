using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Stages.Commands.Reorder
{
    public class ReorderStudyStagesCommandHandler : IRequestHandler<ReorderStudyStagesCommand, List<ProjectStageDto>>
    {
        private readonly IStudyStageService _service;
        private readonly StudyKit _kit;
        private readonly StudyStageSupport _support;

        public ReorderStudyStagesCommandHandler(IStudyStageService service, StudyKit kit, StudyStageSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<List<ProjectStageDto>> Handle(ReorderStudyStagesCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _support.LoadProjectAsync(request.ProjectId, viewer);
            StudyStageSupport.EnsureOpen(project);
            var stages = await _service.GetStagesAsync(project.Id);
            if (request.StageIds.Count != stages.Count || request.StageIds.Distinct().Count() != stages.Count
                || stages.Any(s => !request.StageIds.Contains(s.Id)))
                throw new ArgumentException("أرسل كل مراحل المشروع بالترتيب الجديد");

            // المراحل متتالية: لا تسبق مرحلة مفتوحة مرحلة مغلقة
            var ordered = request.StageIds.Select(id => stages.First(s => s.Id == id)).ToList();
            var firstOpen = ordered.FindIndex(s => s.Status != StudyStageStatus.Closed);
            if (firstOpen >= 0 && ordered.Skip(firstOpen).Any(s => s.Status == StudyStageStatus.Closed))
                throw new InvalidOperationException("المراحل المغلقة تبقى قبل المفتوحة — المراحل متتالية");

            for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i + 1;
            await _service.SaveAsync();
            await _support.LogAsync(project.Id, StudyEntityType.Project, project.Id, StudyLogAction.Edited, $"أعاد ترتيب مراحل المشروع {project.Code}");
            return (await _service.GetStagesAsync(project.Id)).Select(_support.StageDto).ToList();
        }
    }
}
