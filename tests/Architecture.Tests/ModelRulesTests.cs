using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Architecture.Tests;

/// <summary>
/// قواعد نموذج EF (بلا اتصال بقاعدة بيانات): أخطاء وقعنا فيها فعلاً أو تكلّف كثيراً إن وقعت.
/// </summary>
public class ModelRulesTests
{
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        using var db = new DataContext(new DbContextOptionsBuilder<DataContext>().UseSqlServer("Server=.;Database=never_connected;Trusted_Connection=True").Options);
        return (IModel)db.GetService<IDesignTimeModel>().Model;
    }

    /// <summary>
    /// الحذف المتسلسل (الابن يُحذف بحذف أبيه) مسموح للتوابع التي لا معنى لها بلا أبيها فقط.
    /// ما عداها Restrict كي لا يحذف حذفٌ واحد سجلات قرار أو أثر (قرار 2026-09-27: «mostly Restrict»).
    /// إضافة حذف متسلسل جديد قرار واعٍ: أضف السطر هنا مع سبب في رسالة الـ commit، وتأكد أنه ليس مساراً ثانياً إلى الكيان نفسه.
    /// </summary>
    private static readonly HashSet<string> AllowedCascades =
    [
        "AssignedTaskActivity -> AssignedTask", "AssignedTaskAttachment -> AssignedTask", "AssignedTaskAttachmentContent -> AssignedTaskAttachment",
        "AssignedTaskChecklistItem -> AssignedTask", "AssignedTaskLink -> AssignedTask", "AssignedTaskTemplateItem -> AssignedTaskTemplate", "CorrespondenceAttachmentContent -> CorrespondenceAttachment",
        "MaintenanceRequestActivity -> MaintenanceRequest", "MaintenanceTransferRequest -> MaintenanceRequest",
        "MapEntryFile -> MapEntry", "MapEntryFile -> MapLayerField", "MapEntryFileContent -> MapEntryFile", "MapLayerField -> MapLayer",
        "Notification -> User", "ProjectDocumentVersionContent -> ProjectDocumentVersion", "RolePermission -> Permission", "RolePermission -> Role",
        "ScheduleDelayAffectedTask -> ScheduleDelay", "ScheduleExtensionDocument -> ScheduleExtension", "ScheduleExtensionItem -> ScheduleExtension",
        "ScheduleProgressEntry -> ScheduleTask", "ScheduleTask -> ProjectSchedule",
        "SparePartDeviceCompany -> DeviceCompany", "SparePartDeviceCompany -> SparePart", "SparePartDeviceType -> DeviceType", "SparePartDeviceType -> SparePart",
        "ToDoItem -> ToDoList", "ToDoList -> User", "UserSignature -> User", "UserToken -> User", "UserWorkTask -> User", "UserWorkTask -> WorkTask",
        "VacationAttachment -> Vacation", "VacationAttachmentContent -> VacationAttachment", "VacationSegment -> Vacation"
    ];

    [Fact]
    public void Cascade_deletes_are_limited_to_the_known_dependents()
    {
        var actual = Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys())
            .Where(f => f.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.ClientCascade)
            .Select(f => $"{f.DeclaringEntityType.ClrType.Name} -> {f.PrincipalEntityType.ClrType.Name}").ToHashSet();

        var added = actual.Except(AllowedCascades).ToList();
        Assert.True(added.Count == 0,
            $"حذف متسلسل جديد (الاسم الافتراضي في EF لعلاقة مطلوبة هو Cascade — اجعله Restrict إلا لتابع بلا معنى بدون أبيه):\n{Scan.Lines(added)}");

        var removed = AllowedCascades.Except(actual).ToList();
        Assert.True(removed.Count == 0, $"سطور في القائمة لم تعد حذفاً متسلسلاً فعلياً، احذفها:\n{Scan.Lines(removed)}");
    }

    [Fact]
    public void Boolean_and_enum_properties_never_default_to_a_value_other_than_zero()
    {
        // أصل الخطأ (2026-09-29): HasDefaultValue(true) على bool: EF يحذف القيمة false من INSERT فتكتب القاعدة true.
        // الافتراضي المساوي لقيمة النوع (false/0) لا يضرّ.
        var bad = Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => (p.ClrType == typeof(bool) || p.ClrType.IsEnum)
                        && ((p.TryGetDefaultValue(out var value) && value is not null && !IsClrDefault(value)) || p.GetDefaultValueSql() is { } sql && !IsZeroSql(sql)))
            .Select(p => $"{p.DeclaringType.ClrType.Name}.{p.Name}").ToList();
        Assert.True(bad.Count == 0, $"قيمة افتراضية لا تساوي قيمة النوع على bool أو enum (EF لا يرسلها إن ساوت قيمة النوع فتُكتب القيمة الافتراضية بدلها):\n{Scan.Lines(bad)}");
    }

    [Fact]
    public void Every_RowVersion_column_is_an_optimistic_concurrency_token()
    {
        var bad = Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => p.Name == "RowVersion" && !p.IsConcurrencyToken).Select(p => p.DeclaringType.ClrType.Name).ToList();
        Assert.True(bad.Count == 0, $"RowVersion بلا IsRowVersion(): لن يُكتشف تعديل متزامن:\n{Scan.Lines(bad)}");
    }

    [Fact]
    public void Every_entity_has_a_primary_key()
    {
        var bad = Model.GetEntityTypes().Where(e => !e.IsOwned() && e.FindPrimaryKey() == null).Select(e => e.ClrType.Name).ToList();
        Assert.True(bad.Count == 0, $"كيان بلا مفتاح أساسي:\n{Scan.Lines(bad)}");
    }

    private static bool IsClrDefault(object value) => value is false || (value.GetType().IsEnum && Convert.ToInt64(value) == 0);
    private static bool IsZeroSql(string sql) => sql.Trim('(', ')', ' ') is "0" or "CONVERT([bit],(0))";
}
