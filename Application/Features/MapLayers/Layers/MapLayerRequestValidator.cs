using Application.DTOs.Request;
using Domain.Entities;
using FluentValidation;
using static Application.Features.MapLayers.MapLayerRules;

namespace Application.Features.MapLayers.Layers
{
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
                field.RuleFor(f => f.FileKinds).Must(k => MapFileRules.CleanKinds(k).Count > 0)
                    .When(f => f.Type == (int)MapFieldType.Files).WithMessage("اختر صيغة واحدة على الأقل لحقل الملفات");
                field.RuleFor(f => f.MaxFiles).InclusiveBetween(1, MapFileRules.MaxFilesPerField)
                    .When(f => f.Type == (int)MapFieldType.Files).WithMessage($"عدد ملفات الحقل من 1 إلى {MapFileRules.MaxFilesPerField}");
                field.RuleFor(f => f.MaxFileMegabytes).GreaterThan(0)
                    .When(f => f.Type == (int)MapFieldType.Files).WithMessage("حدّد أقصى حجم للملف بالـ MB");
            });
        }
    }
}
