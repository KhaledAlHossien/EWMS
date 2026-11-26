using Application.DTOs.Response;
using AutoMapper;
using Domain.Entities.Maintenance;

namespace Application.Features.Maintenance.Requests
{
    /// <summary>التحويل إلى DTO مع ما يستطيعه المستخدم الحالي على السجل</summary>
    public static class MaintenanceRequestMapping
    {
        public static MaintenanceRequestResponseDto ToDto(IMapper mapper, MaintenanceRequest entity, MaintenanceRules.Scopes scopes)
        {
            var dto = mapper.Map<MaintenanceRequestResponseDto>(entity);
            dto.CanEdit = MaintenanceRules.In(scopes.Edit, entity);
            dto.CanDelete = MaintenanceRules.In(scopes.Delete, entity);
            dto.CanAssign = MaintenanceRules.In(scopes.Assign, entity);
            dto.CanChangeStatus = MaintenanceRules.In(scopes.Status, entity);
            // طلب التحويل: الطلب مسند إليّ وأملك الصلاحية (وجود طلب معلّق يُفحص عند الإرسال)
            dto.CanRequestTransfer = scopes.Transfer != null && entity.UserId == scopes.Transfer.OwnerId;

            // الطلب المُغلق (مُسلَّم / غير قابل للصيانة): للقراءة فقط — الحذف يبقى لصاحب صلاحيته
            dto.IsClosed = MaintenanceRules.IsClosed(entity);
            if (dto.IsClosed) dto.CanEdit = dto.CanChangeStatus = dto.CanAssign = dto.CanRequestTransfer = false;
            return dto;
        }
    }
}
