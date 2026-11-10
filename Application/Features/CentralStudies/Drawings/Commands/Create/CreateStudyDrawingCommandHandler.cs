using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Drawings.Commands.Create
{
    public class CreateStudyDrawingCommandHandler : IRequestHandler<CreateStudyDrawingCommand, StudyDrawingDto>
    {
        private readonly IStudyDrawingService _service;
        private readonly IStudyProjectService _projects;
        private readonly IOfficeService _offices;
        private readonly IDepartmentService _departments;
        private readonly StudyKit _kit;
        private readonly StudyDrawingSupport _support;

        public CreateStudyDrawingCommandHandler(IStudyDrawingService service, IStudyProjectService projects, IOfficeService offices, IDepartmentService departments, StudyKit kit, StudyDrawingSupport support)
        {
            _service = service;
            _projects = projects;
            _offices = offices;
            _departments = departments;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyDrawingDto> Handle(CreateStudyDrawingCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var project = await _projects.GetByIdAsync(request.Dto.ProjectId) ?? throw new KeyNotFoundException("المشروع غير موجود");
            StudyRules.EnsureInScope(viewer, project.BranchId);
            StudyDrawingSupport.EnsureOpen(project);

            var departmentId = viewer.IsOrganizationWide && request.Dto.DepartmentId is int chosen ? chosen
                : viewer.User.DepartmentId ?? throw new InvalidOperationException("حسابك غير مرتبط بقسم — المخطط يصدر باسم قسم");
            if (!await _departments.ExistsAsync(departmentId)) throw new KeyNotFoundException("القسم المُصدِر غير موجود");
            if (!await _projects.DepartmentParticipatesAsync(project.Id, departmentId))
                throw new InvalidOperationException("القسم المُصدِر غير مشارك في المشروع");
            var kind = (DrawingKind)request.Dto.Kind;
            await _support.EnsureBasisAsync(kind, request.Dto.BasedOnVersionId, project.Id);
            var number = request.Dto.DrawingNumber.Trim();
            if (await _service.DrawingNumberExistsAsync(project.Id, number))
                throw new InvalidOperationException("رقم المخطط مستخدم في المشروع");

            var drawing = ProjectDrawing.Create(kind);
            drawing.ProjectId = project.Id;
            drawing.DepartmentId = departmentId;
            drawing.OfficeId = await CreatorOfficeAsync(viewer, departmentId);
            drawing.PreparedByUserId = viewer.Id;
            drawing.BasedOnVersionId = request.Dto.BasedOnVersionId;
            drawing.Name = request.Dto.Name.Trim();
            drawing.DrawingNumber = number;
            drawing.DrawingDate = request.Dto.DrawingDate.Date;
            drawing.Description = StudyRules.Clean(request.Dto.Description);
            drawing.Status = DrawingStatus.InPreparation;
            drawing.CreatedAt = DateTime.Now;

            await _service.AddDrawingAsync(drawing);
            await _support.LogAsync(project.Id, StudyEntityType.Drawing, drawing.Id, StudyLogAction.Created,
                $"أضاف المخطط {StudyRules.Name(kind)} {drawing.DrawingNumber} — {drawing.Name}");
            return await _support.DetailAsync(drawing.Id, viewer);
        }

        /// <summary>
        /// مكتب المُنشئ إن كان تابعاً للقسم المُصدِر، وإلا null (رئيس قسم بلا مكتب، أو منشئ من قسم آخر) — بلا رفض.
        /// يُملأ عند الإنشاء فقط ولا يُقرأ من الطلب ولا يمسّه التعديل. توثيقي: لا أثر له على الصلاحيات أو المراجعة أو الاعتماد.
        /// </summary>
        private async Task<int?> CreatorOfficeAsync(Viewer viewer, int departmentId)
        {
            if (viewer.User.OfficeId is not int officeId) return null;
            var office = await _offices.GetByIdAsync(officeId);
            return office?.DepartmentId == departmentId ? officeId : null;
        }
    }
}
