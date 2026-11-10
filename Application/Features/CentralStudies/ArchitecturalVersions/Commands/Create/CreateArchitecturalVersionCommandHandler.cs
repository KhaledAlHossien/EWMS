using Application.DTOs.Response;
using Application.Features.CentralStudies.Drawings;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.ArchitecturalVersions.Commands.Create
{
    public class CreateArchitecturalVersionCommandHandler : IRequestHandler<CreateArchitecturalVersionCommand, StudyDrawingDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public CreateArchitecturalVersionCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(CreateArchitecturalVersionCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await _support.LoadAsync(request.DrawingId, viewer);
            if (drawing is not ArchitecturalDrawing architectural)
                throw new InvalidOperationException("النسخ للمخطط المعماري فقط");
            StudyDrawingSupport.EnsureOpen(drawing.Project);
            StudyDrawingSupport.EnsureOwnDepartment(viewer, drawing);
            if (architectural.Versions.Any(v => v.Status is DrawingStatus.InPreparation or DrawingStatus.PendingReview))
                throw new InvalidOperationException("للمخطط نسخة قيد الإعداد أو الاعتماد — أكملها قبل إصدار نسخة جديدة");

            var version = new ArchitecturalVersion
            {
                DrawingId = drawing.Id,
                CreatedByUserId = viewer.Id,
                VersionNumber = await _service.NextVersionNumberAsync(drawing.Id),
                VersionDate = request.Dto.VersionDate.Date,
                Content = StudyRules.Clean(request.Dto.Content),
                Status = DrawingStatus.InPreparation,
                CreatedAt = DateTime.Now
            };
            if (drawing.Status != DrawingStatus.Approved) drawing.Status = DrawingStatus.InPreparation;
            await _service.AddVersionAsync(version);
            await _support.LogAsync(drawing.ProjectId, StudyEntityType.Version, version.Id, StudyLogAction.Created,
                $"أصدر النسخة {StudyRules.VersionLabel(version.VersionNumber)} من المخطط {drawing.DrawingNumber}");
            return await _support.DetailAsync(drawing.Id, viewer);
        }
    }
}
