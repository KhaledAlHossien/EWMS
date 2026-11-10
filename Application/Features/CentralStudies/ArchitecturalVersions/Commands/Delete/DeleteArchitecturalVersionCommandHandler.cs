using Application.DTOs.Response;
using Application.Features.CentralStudies.Drawings;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Delete
{
    public class DeleteArchitecturalVersionCommandHandler : IRequestHandler<DeleteArchitecturalVersionCommand, StudyDrawingDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public DeleteArchitecturalVersionCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(DeleteArchitecturalVersionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var version = await _support.LoadVersionForEditAsync(request.Id, viewer);
            if (await _service.VersionUsedAsBasisAsync(version.Id))
                throw new InvalidOperationException("مخططات أخرى مبنية على هذه النسخة");
            var (drawingId, projectId, label) = (version.DrawingId, version.Drawing.ProjectId, StudyRules.VersionLabel(version.VersionNumber));
            await _service.DeleteVersionAsync(version);
            await _support.LogAsync(projectId, StudyEntityType.Version, request.Id, StudyLogAction.Deleted, $"حذف النسخة {label}");
            return await _support.DetailAsync(drawingId, viewer);
        }
    }
}
