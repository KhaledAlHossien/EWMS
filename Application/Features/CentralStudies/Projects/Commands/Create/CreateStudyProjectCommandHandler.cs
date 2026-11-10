using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.Create
{
    public class CreateStudyProjectCommandHandler : IRequestHandler<CreateStudyProjectCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IStudyStartOrderService _startOrders;
        private readonly IStudyAssignmentService _assignments;
        private readonly IStudyLookupService _lookups;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public CreateStudyProjectCommandHandler(IStudyProjectService service, IStudyStartOrderService startOrders, IStudyAssignmentService assignments, IStudyLookupService lookups, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _startOrders = startOrders;
            _assignments = assignments;
            _lookups = lookups;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(CreateStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var order = await _startOrders.GetByIdAsync(request.Dto.StartOrderId) ?? throw new KeyNotFoundException("أمر البدء غير موجود");
            var assignment = order.Response.Assignment;
            var studyRequest = assignment.Request;
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            StudyRules.EnsureApproved(order.Status, "أمر البدء");
            if (await _service.ExistsForStartOrderAsync(order.Id))
                throw new InvalidOperationException("لأمر البدء مشروع مسبقاً — مشروع واحد لكل أمر");

            // الكود يُنسخ من التكليف، ويُطلب هنا إن خلا التكليف منه
            var code = assignment.ProjectCode ?? StudyRules.Clean(request.Dto.Code)
                ?? throw new InvalidOperationException("لا كود مشروع في التكليف — اكتب كود المشروع");
            if (assignment.ProjectCode == null && await _assignments.ProjectCodeExistsAsync(code, assignment.Id))
                throw new InvalidOperationException("كود المشروع مستخدم في تكليف أو مشروع آخر");
            if (await _service.CodeExistsAsync(code))
                throw new InvalidOperationException("كود المشروع مستخدم في مشروع آخر");

            var now = DateTime.Now;
            var project = new StudyProject
            {
                StartOrderId = order.Id,
                BranchId = studyRequest.BranchId,
                SiteId = studyRequest.SiteId,   // يُنسخ من الطلب (للخريطة)
                ManagerUserId = order.ProjectManagerUserId,
                CreatedByUserId = viewer.Id,
                Code = code,
                Name = request.Dto.Name.Trim(),
                PlannedStartDate = request.Dto.PlannedStartDate.Date,
                PlannedEndDate = request.Dto.PlannedEndDate.Date,
                ActualStartDate = (request.Dto.ActualStartDate ?? order.StudyStartDate).Date,
                Status = StudyProjectStatus.Running,
                CreatedAt = now
            };

            // المراحل من الأنواع الفعّالة بترتيبها؛ المرتبطة بمستند مغلقة وتواريخها من ذلك المستند
            var types = await _lookups.GetStageTypesAsync(activeOnly: true);
            var order1 = 0;
            foreach (var type in types)
            {
                var stage = new ProjectStage
                {
                    StageTypeId = type.Id,
                    SortOrder = ++order1,
                    Weight = type.DefaultWeight,
                    Status = StudyStageStatus.NotStarted
                };
                (DateTime Start, DateTime End)? dates = type.LinkedDocument switch
                {
                    StudyLinkedDocument.Request => (studyRequest.RequestDate, (studyRequest.UpdatedAt ?? studyRequest.CreatedAt).Date),
                    StudyLinkedDocument.Assignment => (assignment.AssignmentDate, (assignment.UpdatedAt ?? assignment.CreatedAt).Date),
                    StudyLinkedDocument.Response => (order.Response.ResponseDate, (order.Response.UpdatedAt ?? order.Response.CreatedAt).Date),
                    StudyLinkedDocument.StartOrder => (order.OrderDate, (order.UpdatedAt ?? order.CreatedAt).Date),
                    _ => null
                };
                if (dates is { } range)
                {
                    var (start, end) = range;
                    stage.Status = StudyStageStatus.Closed;
                    stage.PlannedStartDate = stage.ActualStartDate = start;
                    stage.PlannedEndDate = stage.ActualEndDate = end < start ? start : end;
                    stage.ClosedByUserId = viewer.Id;
                    stage.ClosedAt = now;
                }
                project.Stages.Add(stage);
            }

            project.Departments.Add(new ProjectDepartment
            {
                DepartmentId = order.CompetentDepartmentId,
                AddedByUserId = viewer.Id,
                AddedAt = now
            });

            await _service.AddAsync(project);
            await _kit.LogAsync(studyRequest.Id, StudyEntityType.Project, project.Id, StudyLogAction.Created,
                $"أنشأ المشروع {project.Code} — {project.Name}", $"المراحل: {project.Stages.Count}");
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
