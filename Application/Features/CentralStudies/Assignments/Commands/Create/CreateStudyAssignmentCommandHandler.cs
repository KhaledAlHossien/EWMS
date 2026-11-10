using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Assignments.Commands.Create
{
    public class CreateStudyAssignmentCommandHandler : IRequestHandler<CreateStudyAssignmentCommand, StudyAssignmentDto>
    {
        private readonly IStudyAssignmentService _service;
        private readonly IStudyRequestService _requests;
        private readonly IStudyNumberService _numbers;
        private readonly StudyKit _kit;
        private readonly StudyAssignmentSupport _support;

        public CreateStudyAssignmentCommandHandler(IStudyAssignmentService service, IStudyRequestService requests, IStudyNumberService numbers, StudyKit kit, StudyAssignmentSupport support)
        {
            _service = service;
            _requests = requests;
            _numbers = numbers;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyAssignmentDto> Handle(CreateStudyAssignmentCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var studyRequest = await _requests.GetByIdAsync(request.Dto.RequestId) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            StudyRules.EnsureApproved(studyRequest.Status, "الطلب");
            if (await _service.ExistsForRequestAsync(studyRequest.Id))
                throw new InvalidOperationException("للطلب تكليف داخلي مسبقاً — تكليف واحد لكل طلب");
            await _support.ValidateAsync(request.Dto, studyRequest.BranchId, null);

            var entity = new StudyAssignment
            {
                RequestId = studyRequest.Id,
                CreatedByUserId = viewer.Id,
                Status = StudyDocumentStatus.Draft,
                CreatedAt = DateTime.Now
            };
            StudyAssignmentSupport.Apply(request.Dto, entity);
            var year = DateTime.Today.Year;
            entity.AssignmentNumber = StudyRules.FormatNumber(StudyDocumentType.Assignment, year,
                await _numbers.NextAsync(StudyDocumentType.Assignment, year));

            await _service.AddAsync(entity);
            await _kit.LogAsync(entity.RequestId, StudyEntityType.Assignment, entity.Id, StudyLogAction.Created,
                $"أصدر التكليف الداخلي {entity.AssignmentNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }
    }
}
