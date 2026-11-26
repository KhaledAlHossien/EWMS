using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.MapLayers.Layers.Commands.Create
{
    public class CreateMapLayerCommandHandler : IRequestHandler<CreateMapLayerCommand, MapLayerDefinitionDto>
    {
        private readonly IMapLayerService _layers;
        private readonly IBranchService _branches;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public CreateMapLayerCommandHandler(IMapLayerService layers, IBranchService branches, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _branches = branches;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapLayerDefinitionDto> Handle(CreateMapLayerCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var dto = request.Dto;
            // فرع الطبقة: فرع المستخدم، أو أي فرع يختاره من يملك نظرة المؤسسة
            int branchId;
            if (viewer.IsOrganizationWide && dto.BranchId is int chosen) branchId = chosen;
            else branchId = viewer.User.BranchId ?? throw new InvalidOperationException(viewer.IsOrganizationWide
                ? "اختر الفرع الذي تتبع له الطبقة"
                : "حسابك غير مرتبط بفرع، فلا يمكن إنشاء طبقة");
            if (!await _branches.ExistsAsync(branchId)) throw new KeyNotFoundException("الفرع غير موجود");

            var provider = _support.ProviderFor(dto.Source);
            if (provider == null) _support.EnsureFileLimits(dto.Fields);
            var name = dto.Name.Trim();
            if (await _layers.NameExistsAsync(branchId, name, null))
                throw new InvalidOperationException("توجد طبقة بنفس الاسم في هذا الفرع");

            var existing = await _layers.GetAllAsync(branchId, includeInactive: true);
            var now = DateTime.UtcNow;
            var layer = new MapLayer
            {
                BranchId = branchId,
                Name = name,
                Description = dto.Description.Trim(),
                Color = dto.Color.ToUpperInvariant(),
                // طبقة بيانات النظام: بلا حقول ولا سجلات — نقاطها من جدول المزوّد بفلترها
                AllowMultiple = provider == null && dto.AllowMultiple,
                IsActive = dto.IsActive,
                Source = provider?.Key ?? string.Empty,
                SourceFilter = provider == null ? "{}" : MapLayerSupport.FilterJson(provider, dto.SourceFilter),
                SortOrder = existing.Count == 0 ? 0 : existing.Max(l => l.Layer.SortOrder) + 1,
                CreatedByUserId = viewer.Id,
                CreatedAt = now,
                UpdatedAt = now,
                Fields = provider == null ? dto.Fields.Select((f, i) => MapLayerSupport.NewField(f, i)).ToList() : []
            };
            await _layers.AddAsync(layer);
            return await _support.DtoAsync(await _support.LoadAsync(layer.Id, viewer));
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
