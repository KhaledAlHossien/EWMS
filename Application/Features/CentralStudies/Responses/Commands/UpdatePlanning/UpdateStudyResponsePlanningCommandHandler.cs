using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;
using MediatR;

namespace Application.Features.CentralStudies.Responses.Commands.UpdatePlanning
{
    public class UpdateStudyResponsePlanningCommandHandler : IRequestHandler<UpdateStudyResponsePlanningCommand, StudyResponseDto>
    {
        private readonly IStudyResponseService _service;
        private readonly IStudyLookupService _lookups;
        private readonly IStudyApprovalService _approvals;
        private readonly StudyKit _kit;
        private readonly StudyResponseSupport _support;

        public UpdateStudyResponsePlanningCommandHandler(IStudyResponseService service, IStudyLookupService lookups, IStudyApprovalService approvals, StudyKit kit, StudyResponseSupport support)
        {
            _service = service;
            _lookups = lookups;
            _approvals = approvals;
            _kit = kit;
            _support = support;
        }

        public async Task<StudyResponseDto> Handle(UpdateStudyResponsePlanningCommand request, CancellationToken ct)
        {
            var viewer = await _kit.ViewerAsync();
            var entity = await _support.LoadAsync(request.Id, viewer);
            var rows = await _approvals.GetForOwnerAsync(StudyEntityType.Response, entity.Id);
            if (!StudyResponseSupport.CanEditPlanning(viewer, entity, rows))
                throw new InvalidOperationException("جزء التخطيط يُعدَّل بعد توقيع القسم المتابع وقبل توقيع التخطيط، لمن يملك صلاحية تعديله");
            var disciplineIds = request.Dto.StaffEstimates.Select(s => s.DisciplineId).Distinct().ToList();
            if (disciplineIds.Count > 0 && await _lookups.CountActiveDisciplinesAsync(disciplineIds) != disciplineIds.Count)
                throw new KeyNotFoundException("أحد الاختصاصات غير موجود أو غير فعّال");

            ApplyPlanning(request.Dto, entity);
            entity.UpdatedAt = DateTime.Now;
            await _service.UpdateAsync(entity, request.Dto.RowVersion);
            await _kit.LogAsync(entity.Assignment.RequestId, StudyEntityType.Response, entity.Id, StudyLogAction.Edited,
                $"عدّل جزء التخطيط من إشعار الرد {entity.ResponseNumber}");
            return await _support.DetailAsync(entity.Id, viewer);
        }

        /// <summary>جزء التخطيط: المدة، والكادر باختصاصه، والموارد اللوجستية</summary>
        private static void ApplyPlanning(StudyResponsePlanningFormDto dto, StudyAssignmentResponse e)
        {
            e.RequiredDays = dto.RequiredDays;

            var disciplines = dto.StaffEstimates.Select(s => s.DisciplineId).ToHashSet();
            e.StaffEstimates.RemoveAll(s => !disciplines.Contains(s.DisciplineId));
            foreach (var s in dto.StaffEstimates)
            {
                var row = e.StaffEstimates.FirstOrDefault(x => x.DisciplineId == s.DisciplineId);
                if (row == null) e.StaffEstimates.Add(row = new ResponseStaffEstimate { DisciplineId = s.DisciplineId });
                row.StaffCount = s.StaffCount;
            }

            e.LogisticItems.Clear();
            e.LogisticItems.AddRange(dto.LogisticItems.Select(x => new ResponseLogisticItem
            {
                Item = x.Item.Trim(),
                Quantity = x.Quantity,
                Note = StudyRules.Clean(x.Note)
            }));
        }
    }
}
