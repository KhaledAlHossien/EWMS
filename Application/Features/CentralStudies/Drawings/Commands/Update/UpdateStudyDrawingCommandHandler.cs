using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Update
{
    public class UpdateStudyDrawingCommandHandler : IRequestHandler<UpdateStudyDrawingCommand, StudyDrawingDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public UpdateStudyDrawingCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(UpdateStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var drawing = await _support.LoadAsync(request.Id, viewer);
            StudyDrawingSupport.EnsureOpen(drawing.Project);
            StudyDrawingSupport.EnsureOwnDepartment(viewer, drawing);
            if (drawing.Status == DrawingStatus.Approved) throw new InvalidOperationException("المخطط معتمد — لا يُعدَّل");
            await _support.EnsureBasisAsync(drawing.Kind, request.Dto.BasedOnVersionId, drawing.ProjectId);
            var number = request.Dto.DrawingNumber.Trim();
            if (await _service.DrawingNumberExistsAsync(drawing.ProjectId, number, drawing.Id))
                throw new InvalidOperationException("رقم المخطط مستخدم في المشروع");

            drawing.Name = request.Dto.Name.Trim();
            drawing.DrawingNumber = number;
            drawing.DrawingDate = request.Dto.DrawingDate.Date;
            drawing.Description = StudyRules.Clean(request.Dto.Description);
            drawing.BasedOnVersionId = request.Dto.BasedOnVersionId;
            // حالة المعماري تتبع نسخه واعتماداتها؛ غيره يرسله المعدّ للمراجعة
            if (drawing.Kind != DrawingKind.Architectural && request.Dto.Status is int status)
                drawing.Status = (DrawingStatus)status;
            drawing.UpdatedAt = DateTime.Now;
            await _service.UpdateDrawingAsync(drawing, request.Dto.RowVersion);
            await _support.LogAsync(drawing.ProjectId, StudyEntityType.Drawing, drawing.Id, StudyLogAction.Edited,
                $"عدّل المخطط {drawing.DrawingNumber}", StudyRules.Name(drawing.Status));
            return await _support.DetailAsync(drawing.Id, viewer);
        }
    }
}
