using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.Create
{
    public class CreateStudyResponseCommandHandler : IRequestHandler<CreateStudyResponseCommand, StudyResponseDto>
    {
        private readonly IStudyResponseService _service;
        private readonly IStudyAssignmentService _assignments;
        private readonly IStudyNumberService _numbers;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public CreateStudyResponseCommandHandler(IStudyResponseService service, IStudyAssignmentService assignments, IStudyNumberService numbers, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _assignments = assignments;
            _numbers = numbers;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyResponseDto> Handle(CreateStudyResponseCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var assignment = await _assignments.GetByIdAsync(request.Dto.AssignmentId) ?? throw new KeyNotFoundException("التكليف غير موجود");
            StudyRules.EnsureInScope(viewer, assignment.Request.BranchId);
            StudyRules.EnsureApproved(assignment.Status, "التكليف");
            if (await _service.ExistsForAssignmentAsync(assignment.Id))
                throw new InvalidOperationException("للتكليف إشعار رد مسبقاً — الرد لا يُعاد");
            if (!StudyRules.InDepartment(viewer, assignment.FollowingDepartmentId))
                throw new UnauthorizedAccessException("يكتب الرد موظفو القسم المتابع للتكليف");
            await _support.ValidateAsync(request.Dto, assignment.Request.BranchId);

            var year = DateTime.Today.Year;
            var entity = new StudyAssignmentResponse
            {
                ResponseNumber = StudyRules.FormatNumber(StudyDocumentType.Response, year, await _numbers.NextAsync(StudyDocumentType.Response, year)),
                AssignmentId = assignment.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            StudyResponseSupport.Apply(request.Dto, entity);
            await _service.AddAsync(entity);
            await _kit.LogAsync(assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Created,
                $"كتب إشعار الرد على التكليف {assignment.AssignmentNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
