using Application.DTOs.Response;
using Application.Features.CentralStudies.Drawings;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Create
{
    public class CreateDesignApprovalCommandHandler : IRequestHandler<CreateDesignApprovalCommand, DesignApprovalDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly IStudyProjectService _projects;
        private readonly IStudyNumberService _numbers;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public CreateDesignApprovalCommandHandler(IStudyDrawingService service, IStudyProjectService projects, IStudyNumberService numbers, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _projects = projects;
            _numbers = numbers;
            _kit = kit;
            _support = support;
        }

        public async Task<DesignApprovalDto> Handle(CreateDesignApprovalCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var version = await _service.GetVersionAsync(request.Dto.VersionId) ?? throw new KeyNotFoundException("النسخة غير موجودة");
            StudyRules.EnsureInScope(viewer, version.Drawing.Project.BranchId);
            StudyDrawingSupport.EnsureOpen(version.Drawing.Project);
            if (version.DesignApproval != null) throw new InvalidOperationException("للنسخة اعتماد مخططات مسبقاً");
            if (version.Status != DrawingStatus.InPreparation)
                throw new InvalidOperationException("يُصدر الاعتماد لنسخة قيد الإعداد");

            var year = DateTime.Today.Year;
            var cda = new DesignApproval
            {
                VersionId = version.Id,
                CreatedByUserId = viewer.Id,
                CdaNumber = StudyRules.FormatNumber(StudyDocumentType.DesignApproval, year,
                    await _numbers.NextAsync(StudyDocumentType.DesignApproval, year)),
                CdaDate = request.Dto.CdaDate.Date,
                Content = StudyRules.Clean(request.Dto.Content),
                TechnicalNotes = StudyRules.Clean(request.Dto.TechnicalNotes),
                Decision = StudyDecision.Pending,
                CreatedAt = DateTime.Now
            };
            version.Status = DrawingStatus.PendingReview;
            version.Drawing.Status = version.Drawing.Status == DrawingStatus.Approved ? DrawingStatus.Approved : DrawingStatus.PendingReview;
            await _service.AddDesignApprovalAsync(cda);   // يحفظ حالة النسخة والمخطط معه

            await _kit.ResetApprovalsAsync(StudyEntityType.DesignApproval, cda.Id);
            await _service.SaveAsync();

            var requestId = await _projects.GetRequestIdAsync(version.Drawing.ProjectId);
            await _kit.LogAsync(requestId, StudyEntityType.DesignApproval, cda.Id, StudyLogAction.Submitted,
                $"أصدر اعتماد المخططات {cda.CdaNumber} للنسخة {StudyRules.VersionLabel(version.VersionNumber)} من {version.Drawing.DrawingNumber}");
            var (loaded, _) = await _support.LoadCdaAsync(cda.Id, viewer);
            await _kit.NotifyNextSignersAsync(ApprovalOwner.Of(loaded, requestId));
            return await _support.CdaDetailAsync(cda.Id, viewer);
        }
    }
}
