using System.Text.Json;
using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Layers.Commands.Update
{
    public class UpdateMapLayerCommandHandler : IRequestHandler<UpdateMapLayerCommand, MapLayerDefinitionDto>
    {
        private readonly IMapLayerService _layers;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;
        private readonly MapLayerSupport _support;

        public UpdateMapLayerCommandHandler(IMapLayerService layers, IUserService users, IUserPermissionService permissions, MapLayerSupport support)
        {
            _layers = layers;
            _users = users;
            _permissions = permissions;
            _support = support;
        }

        public async Task<MapLayerDefinitionDto> Handle(UpdateMapLayerCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var layer = await _support.LoadAsync(request.Id, viewer);
            var dto = request.Dto;

            if ((dto.Source ?? string.Empty).Trim() != layer.Source)
                throw new InvalidOperationException("مصدر الطبقة لا يتغير بعد إنشائها — أنشئ طبقة جديدة بالمصدر المطلوب");
            var provider = _support.ProviderFor(layer.Source);
            if (provider == null) _support.EnsureFileLimits(dto.Fields);
            var name = dto.Name.Trim();
            if (await _layers.NameExistsAsync(layer.BranchId, name, layer.Id))
                throw new InvalidOperationException("توجد طبقة بنفس الاسم في هذا الفرع");

            // الطبقة أحادية السجل لا تصبح كذلك إن كان لموقع أكثر من سجل فيها (يُتحقق عند الحفظ التالي لكل موقع)
            layer.Name = name;
            layer.Description = dto.Description.Trim();
            layer.Color = dto.Color.ToUpperInvariant();
            layer.AllowMultiple = provider == null && dto.AllowMultiple;
            layer.IsActive = dto.IsActive;
            layer.UpdatedAt = DateTime.UtcNow;
            if (provider != null)
            {
                layer.SourceFilter = MapLayerSupport.FilterJson(provider, dto.SourceFilter);
                await _layers.UpdateAsync(layer, dto.RowVersion);
                return await _support.DtoAsync(await _support.LoadAsync(layer.Id, viewer));
            }

            // الحقول المرسلة: الموجود يُعدَّل، والجديد يُضاف؛ الترتيب = ترتيب القائمة. غير المرسل لا يُمسّ
            for (var i = 0; i < dto.Fields.Count; i++)
            {
                var f = dto.Fields[i];
                if (f.Id is int fieldId)
                {
                    var field = layer.Fields.FirstOrDefault(x => x.Id == fieldId)
                        ?? throw new KeyNotFoundException("أحد الحقول لا يتبع هذه الطبقة");
                    if ((int)field.Type != f.Type && await _layers.FieldUsageAsync(field) > 0)
                        throw new InvalidOperationException($"لا يمكن تغيير نوع الحقل «{field.Label}» وله قيم مسجّلة — أضف حقلاً جديداً بدلاً منه");
                    field.Label = f.Label.Trim();
                    field.Type = (MapFieldType)f.Type;
                    field.IsRequired = f.IsRequired;
                    field.Options = JsonSerializer.Serialize(f.Type == (int)MapFieldType.Select ? CleanOptions(f.Options) : []);
                    MapLayerSupport.ApplyFileSettings(field, f);
                    field.SortOrder = i;
                }
                else
                {
                    layer.Fields.Add(MapLayerSupport.NewField(f, i));
                }
            }
            if (layer.Fields.Count > MaxFields) throw new InvalidOperationException($"الحد الأقصى {MaxFields} حقلاً للطبقة");

            await _layers.UpdateAsync(layer, dto.RowVersion);
            return await _support.DtoAsync(await _support.LoadAsync(layer.Id, viewer));
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);
    }
}
