using System.Reflection;
using MediatR;

namespace Architecture.Tests;

/// <summary>
/// اتجاه الاعتماد (Clean Architecture): API ← Application, Infrastructure؛ Infrastructure ← Application, Domain؛ Application ← Domain.
/// المخالفة تظهر عند البناء بدل أن تتسرب إلى طبقة لا تعرف عنها شيئاً.
/// </summary>
public class LayeringTests
{
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n is not null);

    [Fact]
    public void Domain_depends_on_no_other_layer()
    {
        var bad = References(Scan.Domain).Where(n => n is "Application" or "Infrastructure" or "API").ToList();
        Assert.True(bad.Count == 0, $"طبقة Domain تشير إلى طبقات خارجية:\n{Scan.Lines(bad)}");
    }

    [Fact]
    public void Application_depends_only_on_Domain()
    {
        var bad = References(Scan.Application).Where(n => n is "Infrastructure" or "API").ToList();
        Assert.True(bad.Count == 0, $"طبقة Application تشير إلى طبقات خارجية:\n{Scan.Lines(bad)}");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_API()
    {
        var bad = References(Scan.Infrastructure).Where(n => n is "API").ToList();
        Assert.True(bad.Count == 0, $"طبقة Infrastructure تشير إلى API:\n{Scan.Lines(bad)}");
    }

    [Fact]
    public void Application_never_touches_Entity_Framework_directly()
    {
        // الوصول للبيانات من Infrastructure وحدها خلف واجهات IXxxService؛ والمعالجات لا تعرف EF
        var ef = References(Scan.Application).Where(n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)).ToList();
        Assert.True(ef.Count == 0, $"طبقة Application تشير إلى EF Core مباشرة:\n{Scan.Lines(ef)}");
    }

    [Fact]
    public void Every_request_has_exactly_one_handler()
    {
        var types = Scan.Application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }).ToList();
        var problems = new List<string>();

        foreach (var request in types.Where(t => typeof(IBaseRequest).IsAssignableFrom(t)))
        {
            var responses = request.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(i => i.GetGenericArguments()[0]).ToList();

            var handlerInterface = responses.Count > 0
                ? typeof(IRequestHandler<,>).MakeGenericType(request, responses[0])
                : typeof(IRequestHandler<>).MakeGenericType(request);

            var handlers = types.Count(t => handlerInterface.IsAssignableFrom(t));
            if (handlers != 1) problems.Add($"{request.Name}: {handlers} معالج");
        }

        Assert.True(problems.Count == 0, $"كل أمر أو استعلام يحتاج معالجاً واحداً بالضبط (غيابه خطأ 500 عند التشغيل، وتعدّده لا يُقبل):\n{Scan.Lines(problems)}");
    }

    /// <summary>
    /// دين معلن: معالجات الوحدات القديمة التي تخدم عدة طلبات. الوحدات الجديدة (المشاريع والجداول) مقسّمة معالجاً لكل حالة استخدام.
    /// تُقسَّم عند تعديل وحدتها لسبب آخر ثم يُحذف اسمها هنا (يفشل الاختبار إن بقي اسم بلا حاجة).
    /// </summary>
    private static readonly HashSet<string> LegacyMultiRequestHandlers =
    [
        "AssignedTaskCommandsHandler", "AssignedTaskExtrasHandler", "AssignedTaskQueriesHandler", "DamageTypeHandler", "DashboardQueriesHandler",
        "DeviceCompanyHandler", "DeviceHandler", "DeviceMaintenanceHandler", "DeviceTypeHandler", "InstallationHandler",
        "MaintenanceRequestCommandsHandler", "MaintenanceRequestQueriesHandler", "MaintenanceRequestStatusHandler", "MaintenanceTaskHandler",
        "MaintenanceTransferHandler", "MapEntryFileHandler", "MapEntryHandler", "MapLayerHandler", "PublicHolidayHandler", "ReportHandler",
        "SiteHandler", "SparePartHandler", "TaskLinksHandler", "TaskRecurrencesHandler", "TaskTemplatesHandler", "ToDoItemHandler",
        "ToDoListHandler", "UserSignatureHandler"
    ];

    private static Dictionary<string, int> RequestsPerHandler() => Scan.Application.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false })
        .Select(t => (t.Name, Count: t.GetInterfaces().Count(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))))
        .Where(x => x.Count > 0)
        .ToDictionary(x => x.Name, x => x.Count);

    [Fact]
    public void A_new_handler_class_serves_exactly_one_request()
    {
        // معالج واحد كبير يطبّق عشرات الواجهات يصعب اختباره ومراجعته: القاعدة معالج لكل حالة استخدام، والمشترك في سياق أو دوال ساكنة
        var problems = RequestsPerHandler().Where(x => x.Value > 1 && !LegacyMultiRequestHandlers.Contains(x.Key))
            .Select(x => $"{x.Key}: {x.Value} طلبات").ToList();

        Assert.True(problems.Count == 0, $"المعالج يخدم أكثر من طلب واحد (قسّمه معالجاً لكل حالة استخدام):\n{Scan.Lines(problems)}");
    }

    [Fact]
    public void The_legacy_multi_request_handler_list_has_no_stale_entries()
    {
        var counts = RequestsPerHandler();
        var stale = LegacyMultiRequestHandlers.Where(n => !counts.TryGetValue(n, out var c) || c <= 1).ToList();
        Assert.True(stale.Count == 0, $"معالجات في قائمة الدين صارت مقسّمة أو لم تعد موجودة، احذفها من القائمة:\n{Scan.Lines(stale)}");
    }
}
