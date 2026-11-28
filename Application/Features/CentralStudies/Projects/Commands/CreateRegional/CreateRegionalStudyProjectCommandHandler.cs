using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Projects.Commands.CreateRegional
{
    /// <summary>
    /// الإحالة إلى الفرع الجغرافي (قرار المستخدم 2026-11-28): الطلب المعتمد لفرع دراسة بمسار التنسيق يصبح مشروعاً مباشرة،
    /// كوده برمز فرع الدراسة وسنة الإحالة، ومراحله من أنواع مسار التنسيق. المدير رئيس قسم التنسيق والارتباط
    /// (من يملك التوقيع بصفته، StudyProjectSupport.CoordinationManagerAsync)، وقسمه مسؤول عن المراحل ومشارك في المشروع. الإنجاز يوقّعه التنسيق والارتباط ثم ضبط الجودة.
    /// </summary>
    public class CreateRegionalStudyProjectCommandHandler : IRequestHandler<CreateRegionalStudyProjectCommand, StudyProjectDto>
    {
        private readonly IStudyProjectService _service;
        private readonly IStudyRequestService _requests;
        private readonly IStudyAssignmentService _assignments;
        private readonly IStudyLookupService _lookups;
        private readonly StudyKit _kit;
        private readonly StudyProjectSupport _support;

        public CreateRegionalStudyProjectCommandHandler(IStudyProjectService service, IStudyRequestService requests, IStudyAssignmentService assignments,
            IStudyLookupService lookups, StudyKit kit, StudyProjectSupport support)
        {
            _service = service;
            _requests = requests;
            _assignments = assignments;
            _lookups = lookups;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyProjectDto> Handle(CreateRegionalStudyProjectCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            StudyRules.Require(viewer, StudyPermissions.CreateRegionalProject);
            var dto = request.Dto;
            var studyRequest = await _requests.GetByIdAsync(dto.RequestId) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, studyRequest.BranchId);
            StudyRules.EnsureApproved(studyRequest.Status, "الطلب");
            if (StudyRules.TrackOf(studyRequest) != StudyTrack.Coordination)
                throw new InvalidOperationException("طلب المركزية يُتابَع بالتكليف الداخلي — الإحالة لطلبات الأفرع الجغرافية فقط");
            if (await _service.ExistsForRequestAsync(studyRequest.Id))
                throw new InvalidOperationException("للدراسة مشروع مسبقاً — مشروع واحد لكل طلب");

            var code = StudyRules.FormatProjectCode(studyRequest, dto.ReferralDate, dto.AdminArchiveNumber);
            if (await _assignments.ProjectCodeExistsAsync(code))
                throw new InvalidOperationException($"كود المشروع «{code}» مستخدم في تكليف أو مشروع آخر — تحقق من رقم أرشيف ديوان الإدارة وتاريخ الإحالة");

            // المدير رئيس قسم التنسيق والارتباط، وقسمه مسؤول عن المراحل (يدير مهامها) ومشارك في المشروع
            var manager = await _support.CoordinationManagerAsync(studyRequest.BranchId, dto.ManagerUserId, viewer);
            var managerId = manager.Id;
            int? departmentId = manager.DepartmentId;

            var types = (await _lookups.GetStageTypesAsync(activeOnly: true)).Where(t => t.Track == StudyTrack.Coordination).ToList();
            if (types.Count == 0)
                throw new InvalidOperationException("لا مراحل فعّالة لمسار التنسيق — عرّفها من أنواع المراحل قبل الإحالة");

            var now = DateTime.Now;
            var referral = dto.ReferralDate.Date;
            var project = new StudyProject
            {
                RequestId = studyRequest.Id,
                BranchId = studyRequest.BranchId,
                SiteId = studyRequest.SiteId,   // يُنسخ من الطلب (للخريطة)
                ManagerUserId = managerId,
                CreatedByUserId = viewer.Id,
                Code = code,
                Name = dto.Name.Trim(),
                PlannedStartDate = dto.PlannedStartDate.Date,
                PlannedEndDate = dto.PlannedEndDate.Date,
                ActualStartDate = referral,
                Status = StudyProjectStatus.Running,
                CreatedAt = now
            };

            var sort = 0;
            foreach (var type in types)
            {
                var stage = new ProjectStage
                {
                    StageTypeId = type.Id,
                    SortOrder = ++sort,
                    Weight = type.DefaultWeight,
                    ResponsibleDepartmentId = departmentId,
                    Status = StudyStageStatus.NotStarted
                };
                // مرحلة مرتبطة بالطلب (المستند الوحيد في هذا المسار) تُنشأ مغلقة بتواريخه
                if (type.LinkedDocument == StudyLinkedDocument.Request)
                {
                    var start = studyRequest.RequestDate.Date;
                    var end = (studyRequest.UpdatedAt ?? studyRequest.CreatedAt).Date;
                    stage.Status = StudyStageStatus.Closed;
                    stage.PlannedStartDate = stage.ActualStartDate = start;
                    stage.PlannedEndDate = stage.ActualEndDate = end < start ? start : end;
                    stage.ClosedByUserId = viewer.Id;
                    stage.ClosedAt = now;
                }
                project.Stages.Add(stage);
            }

            if (departmentId is int department)
                project.Departments.Add(new ProjectDepartment { DepartmentId = department, AddedByUserId = viewer.Id, AddedAt = now });

            await _service.AddAsync(project);
            await _kit.LogAsync(studyRequest.Id, StudyEntityType.Project, project.Id, StudyLogAction.Created,
                $"أحال الدراسة إلى فرع {studyRequest.StudyBranch.Name} — أنشأ المشروع {project.Code}",
                $"{project.Name} — المراحل: {project.Stages.Count}");
            return await _support.DetailAsync(project.Id, viewer);
        }
    }
}
