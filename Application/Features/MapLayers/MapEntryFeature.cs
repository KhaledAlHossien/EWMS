using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers
{
    public record GetSiteEntriesQuery(int SiteId) : IRequest<SiteEntriesDto>;
    public record CreateMapEntryCommand(MapEntryRequestDto Dto) : IRequest<MapEntryDto>;
    public record UpdateMapEntryCommand(int Id, MapEntryRequestDto Dto) : IRequest<MapEntryDto>;
    public record DeleteMapEntryCommand(int Id) : IRequest<Unit>;

    public class CreateMapEntryCommandValidator : AbstractValidator<CreateMapEntryCommand>
    {
        public CreateMapEntryCommandValidator()
        {
            RuleFor(x => x.Dto.LayerId).GreaterThan(0).WithMessage("اختر الطبقة");
            RuleFor(x => x.Dto.SiteId).GreaterThan(0).WithMessage("اختر الموقع");
        }
    }

    public class MapEntryHandler :
        IRequestHandler<GetSiteEntriesQuery, SiteEntriesDto>,
        IRequestHandler<CreateMapEntryCommand, MapEntryDto>,
        IRequestHandler<UpdateMapEntryCommand, MapEntryDto>,
        IRequestHandler<DeleteMapEntryCommand, Unit>
    {
        private readonly IMapEntryService _entries;
        private readonly IMapEntryFileService _files;
        private readonly IMapLayerService _layers;
        private readonly ISiteService _sites;
        private readonly IDepartmentService _departments;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public MapEntryHandler(IMapEntryService entries, IMapEntryFileService files, IMapLayerService layers, ISiteService sites,
            IDepartmentService departments, IUserService users, IUserPermissionService permissions)
        {
            _entries = entries;
            _files = files;
            _layers = layers;
            _sites = sites;
            _departments = departments;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<MapEntry> LoadAsync(int id, Viewer viewer)
        {
            var entry = await _entries.GetByIdAsync(id) ?? throw new KeyNotFoundException("السجل غير موجود");
            EnsureEntry(viewer, entry);
            return entry;
        }

        private async Task<MapEntryDto> DtoAsync(int id, Viewer viewer) =>
            ToDto(await _entries.GetByIdAsync(id) ?? throw new KeyNotFoundException("السجل غير موجود"), viewer);

        /// <summary>
        /// القسم المالك للسجل داخل فرع الطبقة: القسم المطلوب إن كان مسموحاً (نظرة المؤسسة، أو ViewBranchMapEntries في فرعه)،
        /// وإلا قسم المستخدم إن كان من فرع الطبقة، وإلا بلا قسم (سجل على مستوى الفرع).
        /// </summary>
        private async Task<int?> DepartmentAsync(Viewer viewer, MapLayer layer, int? requestedDepartmentId)
        {
            if (requestedDepartmentId is int departmentId && departmentId != viewer.User.DepartmentId)
            {
                var department = await _departments.GetByIdAsync(departmentId) ?? throw new KeyNotFoundException("القسم غير موجود");
                if (department.BranchId != layer.BranchId)
                    throw new InvalidOperationException("القسم المختار لا يتبع فرع الطبقة");
                var allowed = viewer.IsOrganizationWide
                    || (viewer.Has(AppPermissions.ViewBranchMapEntries) && OrganizationRole.InBranch(viewer.User, layer.BranchId));
                if (!allowed) throw new UnauthorizedAccessException("لا يمكنك تسجيل سجل باسم قسم آخر");
                return department.Id;
            }
            return OrganizationRole.InBranch(viewer.User, layer.BranchId) ? viewer.User.DepartmentId : null;
        }

        public async Task<SiteEntriesDto> Handle(GetSiteEntriesQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            if (!await _sites.ExistsAsync(request.SiteId)) throw new KeyNotFoundException("الموقع غير موجود");

            // الطبقات المتاحة للإضافة والعرض: طبقات فرعي الفعّالة، أو كل الفروع لنظرة المؤسسة
            int? branchId = viewer.IsOrganizationWide ? null : viewer.User.BranchId;
            var layers = !viewer.IsOrganizationWide && branchId == null
                ? []
                : (await _layers.GetAllAsync(branchId, includeInactive: false))
                    .Where(l => l.Layer.Source.Length == 0)   // طبقات بيانات النظام لا تُضاف إليها سجلات
                    .Select(l => ToDto(l.Layer, l.Entries, includeHidden: false)).ToList();

            var entries = await _entries.GetForSiteAsync(request.SiteId, EntryScopeFor(viewer));
            var canCreate = viewer.Has("CreateMapEntry") && layers.Count > 0;

            // طبقات السجلات المعروضة وإن كانت معطّلة: عناوين حقولها لعرض القيم (للقراءة فقط — IsActive=false)
            foreach (var layer in entries.Select(e => e.Layer).DistinctBy(l => l.Id))
                if (!layers.Any(l => l.Id == layer.Id)) layers.Add(ToDto(layer, 0, includeHidden: false));

            return new SiteEntriesDto
            {
                Layers = layers,
                Entries = entries.Select(e => ToDto(e, viewer)).ToList(),
                CanCreate = canCreate
            };
        }

        public async Task<MapEntryDto> Handle(CreateMapEntryCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var dto = request.Dto;
            var layer = await _layers.GetByIdAsync(dto.LayerId) ?? throw new KeyNotFoundException("الطبقة غير موجودة");
            EnsureLayer(viewer, layer);
            if (layer.Source.Length > 0)
                throw new InvalidOperationException("هذه الطبقة من بيانات النظام — نقاطها تُقرأ من جداولها ولا تُضاف إليها سجلات يدوياً");
            if (!layer.IsActive) throw new InvalidOperationException("الطبقة معطّلة — لا تُضاف إليها سجلات");
            if (!await _sites.ExistsAsync(dto.SiteId)) throw new KeyNotFoundException("الموقع غير موجود");
            if (!layer.AllowMultiple && await _entries.ExistsAsync(layer.Id, dto.SiteId, null))
                throw new InvalidOperationException($"لهذا الموقع سجل في طبقة «{layer.Name}» — عدّله بدل إضافة سجل آخر");

            var entry = new MapEntry
            {
                LayerId = layer.Id,
                SiteId = dto.SiteId,
                BranchId = layer.BranchId,
                DepartmentId = await DepartmentAsync(viewer, layer, dto.DepartmentId),
                Values = NormalizeValues(layer, dto.Values, null),
                CreatedByUserId = viewer.Id,
                CreatedAt = DateTime.UtcNow
            };
            var files = await MapEntryFiles.PlanAsync(layer, null, dto.Values, viewer, _files);
            await _entries.AddAsync(entry, files);
            return await DtoAsync(entry.Id, viewer);
        }

        public async Task<MapEntryDto> Handle(UpdateMapEntryCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var entry = await LoadAsync(request.Id, viewer);
            if (!entry.Layer.IsActive) throw new InvalidOperationException("الطبقة معطّلة — سجلاتها للقراءة فقط");

            entry.Values = NormalizeValues(entry.Layer, request.Dto.Values, entry.Values);
            var files = await MapEntryFiles.PlanAsync(entry.Layer, entry, request.Dto.Values, viewer, _files);
            if (request.Dto.DepartmentId is int requested && requested != entry.DepartmentId)
                entry.DepartmentId = await DepartmentAsync(viewer, entry.Layer, requested);
            entry.UpdatedByUserId = viewer.Id;
            entry.UpdatedAt = DateTime.UtcNow;
            await _entries.UpdateAsync(entry, request.Dto.RowVersion, files);
            return await DtoAsync(entry.Id, viewer);
        }

        public async Task<Unit> Handle(DeleteMapEntryCommand request, CancellationToken ct)
        {
            var entry = await LoadAsync(request.Id, await CurrentAsync());
            await _entries.DeleteAsync(entry);
            return Unit.Value;
        }
    }
}
