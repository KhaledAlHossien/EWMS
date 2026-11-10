using Application.DTOs.Response;
using Application.Features.CentralStudies.Drawings;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Update
{
    public class UpdateArchitecturalVersionCommandHandler : IRequestHandler<UpdateArchitecturalVersionCommand, StudyDrawingDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public UpdateArchitecturalVersionCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(UpdateArchitecturalVersionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var version = await _support.LoadVersionForEditAsync(request.Id, viewer);
            version.VersionDate = request.Dto.VersionDate.Date;
            version.Content = StudyRules.Clean(request.Dto.Content);
            await _service.UpdateVersionAsync(version, request.Dto.RowVersion);
            await _support.LogAsync(version.Drawing.ProjectId, StudyEntityType.Version, version.Id, StudyLogAction.Edited,
                $"عدّل النسخة {StudyRules.VersionLabel(version.VersionNumber)} من المخطط {version.Drawing.DrawingNumber}");
            return await _support.DetailAsync(version.DrawingId, viewer);
        }
    }
}
