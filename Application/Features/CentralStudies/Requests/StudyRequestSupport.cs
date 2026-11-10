using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Requests
{
    /// <summary>الدوال المشتركة بين عمليات Requests (تحميل السجل، التحويل إلى DTO، الأعلام…) — منقولة من المعالج الواحد القديم</summary>
    public class StudyRequestSupport
    {
        private readonly IStudyRequestService _service;
        private readonly ISiteService _sites;
        private readonly StudyKit _kit;

        public StudyRequestSupport(IStudyRequestService service, ISiteService sites, StudyKit kit)
        {
            _service = service;
            _sites = sites;
            _kit = kit;
        }

        public StudyRequestDto ToDto(StudyRequest entity, Viewer viewer)
        {
            var dto = _kit.Mapper.Map<StudyRequestDto>(entity);
            var inScope = StudyRules.InScope(viewer, entity.BranchId);
            dto.CanEdit = inScope && viewer.Has("EditStudyRequest") && StudyRules.IsEditable(entity.Status);
            dto.CanSubmit = dto.CanEdit;
            dto.CanDelete = inScope && viewer.Has("DeleteStudyRequest") && entity.Status == StudyDocumentStatus.Draft;
            dto.CanCancel = inScope && viewer.Has(StudyPermissions.CancelDocuments)
                            && entity.Status != StudyDocumentStatus.Cancelled && entity.Status != StudyDocumentStatus.Draft
                            && (entity.Assignment == null || entity.Assignment.Status == StudyDocumentStatus.Cancelled);
            dto.CanCreateAssignment = inScope && viewer.Has("CreateStudyAssignment")
                                      && entity.Status == StudyDocumentStatus.Approved && entity.Assignment == null;
            return dto;
        }

        public async Task EnsureSiteAsync(int? siteId)
        {
            if (siteId is int id && !await _sites.ExistsAsync(id))
                throw new KeyNotFoundException("الموقع المحدد غير موجود في سجل المواقع");
        }

        public static void Normalize(StudyRequest e)
        {
            e.MainBeneficiary = e.MainBeneficiary.Trim();
            e.SubBeneficiary = StudyRules.Clean(e.SubBeneficiary);
            e.DiwanRequestNumber = StudyRules.Clean(e.DiwanRequestNumber);
            e.MinistryArchiveNumber = StudyRules.Clean(e.MinistryArchiveNumber);
            e.AdminArchiveNumber = StudyRules.Clean(e.AdminArchiveNumber);
            e.BeneficiaryArchiveNumber = StudyRules.Clean(e.BeneficiaryArchiveNumber);
            e.EntityCode = StudyRules.Clean(e.EntityCode);
            e.Area = StudyRules.Clean(e.Area);
            e.CityOrVillage = StudyRules.Clean(e.CityOrVillage);
            e.PropertyNature = StudyRules.Clean(e.PropertyNature);
            e.FollowerName = StudyRules.Clean(e.FollowerName);
            e.FollowerPhone = PhoneRules.ToStored(e.FollowerPhone);
            e.Notes = StudyRules.Clean(e.Notes);
            e.GovernorateCode = e.GovernorateCode.Trim().ToUpperInvariant();
            e.MinistryArchiveDate = e.MinistryArchiveDate?.Date;
            e.AdminArchiveDate = e.AdminArchiveDate?.Date;
            e.AddedDate = e.AddedDate?.Date;
        }

        public async Task<StudyRequest> LoadAsync(int id, Viewer viewer)
        {
            var entity = await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("الطلب غير موجود");
            StudyRules.EnsureInScope(viewer, entity.BranchId);
            return entity;
        }

        public async Task<StudyRequestDto> DetailAsync(int id, Viewer viewer)
        {
            var entity = await LoadAsync(id, viewer);
            var dto = ToDto(entity, viewer);
            dto.Chain = await _kit.ChainAsync(entity.Id);
            await _kit.AddWhoIsNextAsync(dto, StudyEntityType.Request, entity.Id);
            return dto;
        }
    }
}
