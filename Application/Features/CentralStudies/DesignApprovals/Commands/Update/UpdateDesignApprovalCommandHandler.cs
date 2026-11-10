using Application.DTOs.Response;
using Application.Features.CentralStudies.Drawings;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Update
{
    public class UpdateDesignApprovalCommandHandler : IRequestHandler<UpdateDesignApprovalCommand, DesignApprovalDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public UpdateDesignApprovalCommandHandler(IStudyDrawingService service, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _kit = kit;
            _support = support;
        }

        public async Task<DesignApprovalDto> Handle(UpdateDesignApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var (cda, requestId) = await _support.LoadCdaAsync(request.Id, viewer);
            StudyDrawingSupport.EnsureOpen(cda.Version.Drawing.Project);
            await _support.EnsureNoDecisionsAsync(cda);

            cda.CdaDate = request.Dto.CdaDate.Date;
            cda.Content = StudyRules.Clean(request.Dto.Content);
            cda.TechnicalNotes = StudyRules.Clean(request.Dto.TechnicalNotes);
            cda.UpdatedAt = DateTime.Now;
            await _service.UpdateDesignApprovalAsync(cda, request.Dto.RowVersion);
            await _kit.LogAsync(requestId, StudyEntityType.DesignApproval, cda.Id, StudyLogAction.Edited, $"عدّل اعتماد المخططات {cda.CdaNumber}");
            return await _support.CdaDetailAsync(cda.Id, viewer);
        }
    }
}
