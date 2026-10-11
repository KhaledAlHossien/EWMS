using System.Text.Json;
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
    /// <summary>BranchId: لنظرة المؤسسة (null = كل الفروع)؛ لغيرها فرع المستخدم دائماً</summary>
    public record GetMapLayersQuery(int? BranchId, bool IncludeInactive) : IRequest<List<MapLayerDefinitionDto>>;
    public record GetMapLayerQuery(int Id) : IRequest<MapLayerDefinitionDto>;
    public record CreateMapLayerCommand(MapLayerRequestDto Dto) : IRequest<MapLayerDefinitionDto>;
    public record UpdateMapLayerCommand(int Id, MapLayerRequestDto Dto) : IRequest<MapLayerDefinitionDto>;
    public record DeleteMapLayerCommand(int Id) : IRequest<Unit>;
    public record GetFieldUsageQuery(int FieldId) : IRequest<FieldUsageDto>;
    /// <summary>حقل بلا قيم: يُحذف. له قيم: Purge=false يخفيه (تبقى القيم)، Purge=true يحذفه مع قيمه</summary>
    public record DeleteMapLayerFieldCommand(int FieldId, bool Purge) : IRequest<MapLayerDefinitionDto>;
    public record RestoreMapLayerFieldCommand(int FieldId) : IRequest<MapLayerDefinitionDto>;

    public class MapLayerRequestValidator : AbstractValidator<MapLayerRequestDto>
    {
        public MapLayerRequestValidator()
        {
            RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم الطبقة مطلوب")
                .MaximumLength(100).WithMessage("اسم الطبقة لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
            RuleFor(x => x.Color).Must(IsColor).WithMessage("اللون غير صحيح (#RRGGBB)");
            RuleFor(x => x.Fields).Must(f => f.Count <= MaxFields).WithMessage($"الحد الأقصى {MaxFields} حقلاً للطبقة");
            RuleForEach(x => x.Fields).ChildRules(field =>
            {
                field.RuleFor(f => f.Label).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("عنوان الحقل مطلوب")
                    .MaximumLength(100).WithMessage("عنوان الحقل لا يتجاوز 100 حرف");
                field.RuleFor(f => f.Type).Must(t => Enum.IsDefined(typeof(MapFieldType), t)).WithMessage("نوع الحقل غير صحيح");
                field.RuleFor(f => f.Options).Must(o => CleanOptions(o).Count > 0)
                    .When(f => f.Type == (int)MapFieldType.Select).WithMessage("حقل القائمة يحتاج خياراً واحداً على الأقل");
            });
        }
    }

    public class CreateMapLayerCommandValidator : AbstractValidator<CreateMapLayerCommand>
    {
        public CreateMapLayerCommandValidator() => RuleFor(x => x.Dto).SetValidator(new MapLayerRequestValidator());
    }

    public class UpdateMapLayerCommandValidator : AbstractValidator<UpdateMapLayerCommand>
    {
        public UpdateMapLayerCommandValidator() => RuleFor(x => x.Dto).SetValidator(new MapLayerRequestValidator());
    }

    public class MapLayerHandler :
        IRequestHandler<GetMapLayersQuery, List<MapLayerDefinitionDto>>,
        IRequestHandler<GetMapLayerQuery, MapLayerDefinitionDto>,
        IRequestHandler<CreateMapLayerCommand, MapLayerDefinitionDto>,
        IRequestHandler<UpdateMapLayerCommand, MapLayerDefinitionDto>,
        IRequestHandler<DeleteMapLayerCommand, Unit>,
        IRequestHandler<GetFieldUsageQuery, FieldUsageDto>,
        IRequestHandler<DeleteMapLayerFieldCommand, MapLayerDefinitionDto>,
        IRequestHandler<RestoreMapLayerFieldCommand, MapLayerDefinitionDto>
    {
        private readonly IMapLayerService _layers;
        private readonly IMapEntryService _entries;
        private readonly IBranchService _branches;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public MapLayerHandler(IMapLayerService layers, IMapEntryService entries, IBranchService branches,
            IUserService users, IUserPermissionService permissions)
        {
            _layers = layers;
            _entries = entries;
            _branches = branches;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private async Task<MapLayer> LoadAsync(int id, Viewer viewer)
        {
            var layer = await _layers.GetByIdAsync(id) ?? throw new KeyNotFoundException("الطبقة غير موجودة");
            EnsureLayer(viewer, layer);
            return layer;
        }

        private async Task<MapLayerDefinitionDto> DtoAsync(MapLayer layer) =>
            ToDto(layer, await _entries.CountForLayerAsync(layer.Id), includeHidden: true);

        private static MapLayerField NewField(MapLayerFieldRequestDto dto, int order) => new()
        {
            Label = dto.Label.Trim(),
            Type = (MapFieldType)dto.Type,
            IsRequired = dto.IsRequired,
            Options = JsonSerializer.Serialize(dto.Type == (int)MapFieldType.Select ? CleanOptions(dto.Options) : []),
            SortOrder = order
        };

        public async Task<List<MapLayerDefinitionDto>> Handle(GetMapLayersQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            int? branchId;
            if (viewer.IsOrganizationWide) branchId = request.BranchId;
            else if (viewer.User.BranchId is int own) branchId = own;
            else return [];

            return (await _layers.GetAllAsync(branchId, request.IncludeInactive))
                .Select(l => ToDto(l.Layer, l.Entries, includeHidden: true)).ToList();
        }

        public async Task<MapLayerDefinitionDto> Handle(GetMapLayerQuery request, CancellationToken ct) =>
            await DtoAsync(await LoadAsync(request.Id, await CurrentAsync()));

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
                AllowMultiple = dto.AllowMultiple,
                IsActive = dto.IsActive,
                SortOrder = existing.Count == 0 ? 0 : existing.Max(l => l.Layer.SortOrder) + 1,
                CreatedByUserId = viewer.Id,
                CreatedAt = now,
                UpdatedAt = now,
                Fields = dto.Fields.Select((f, i) => NewField(f, i)).ToList()
            };
            await _layers.AddAsync(layer);
            return await DtoAsync(await LoadAsync(layer.Id, viewer));
        }

        public async Task<MapLayerDefinitionDto> Handle(UpdateMapLayerCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var layer = await LoadAsync(request.Id, viewer);
            var dto = request.Dto;

            var name = dto.Name.Trim();
            if (await _layers.NameExistsAsync(layer.BranchId, name, layer.Id))
                throw new InvalidOperationException("توجد طبقة بنفس الاسم في هذا الفرع");

            // الطبقة أحادية السجل لا تصبح كذلك إن كان لموقع أكثر من سجل فيها (يُتحقق عند الحفظ التالي لكل موقع)
            layer.Name = name;
            layer.Description = dto.Description.Trim();
            layer.Color = dto.Color.ToUpperInvariant();
            layer.AllowMultiple = dto.AllowMultiple;
            layer.IsActive = dto.IsActive;
            layer.UpdatedAt = DateTime.UtcNow;

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
                    field.SortOrder = i;
                }
                else
                {
                    layer.Fields.Add(NewField(f, i));
                }
            }
            if (layer.Fields.Count > MaxFields) throw new InvalidOperationException($"الحد الأقصى {MaxFields} حقلاً للطبقة");

            await _layers.UpdateAsync(layer, dto.RowVersion);
            return await DtoAsync(await LoadAsync(layer.Id, viewer));
        }

        public async Task<Unit> Handle(DeleteMapLayerCommand request, CancellationToken ct)
        {
            var layer = await LoadAsync(request.Id, await CurrentAsync());
            var count = await _entries.CountForLayerAsync(layer.Id);
            if (count > 0)
                throw new InvalidOperationException($"على الطبقة {count} سجلاً — احذف سجلاتها أولاً، أو عطّل الطبقة لإخفائها مع إبقاء سجلاتها");
            await _layers.DeleteAsync(layer);
            return Unit.Value;
        }

        private async Task<MapLayerField> LoadFieldAsync(int fieldId, Viewer viewer)
        {
            var field = await _layers.GetFieldAsync(fieldId) ?? throw new KeyNotFoundException("الحقل غير موجود");
            EnsureLayer(viewer, field.Layer);
            return field;
        }

        public async Task<FieldUsageDto> Handle(GetFieldUsageQuery request, CancellationToken ct)
        {
            var field = await LoadFieldAsync(request.FieldId, await CurrentAsync());
            return new FieldUsageDto { Count = await _layers.FieldUsageAsync(field) };
        }

        public async Task<MapLayerDefinitionDto> Handle(DeleteMapLayerFieldCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var field = await LoadFieldAsync(request.FieldId, viewer);
            var layerId = field.LayerId;
            var used = await _layers.FieldUsageAsync(field) > 0;

            if (used && !request.Purge)
            {
                field.IsHidden = true;              // إخفاء: القيم محفوظة ويمكن إعادته
                await _layers.SaveChangesAsync();
            }
            else
            {
                await _layers.PurgeFieldAsync(field);   // بلا قيم، أو حذف نهائي مع قيمه
            }
            return await DtoAsync(await LoadAsync(layerId, viewer));
        }

        public async Task<MapLayerDefinitionDto> Handle(RestoreMapLayerFieldCommand request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var field = await LoadFieldAsync(request.FieldId, viewer);
            field.IsHidden = false;
            await _layers.SaveChangesAsync();
            return await DtoAsync(await LoadAsync(field.LayerId, viewer));
        }
    }
}
