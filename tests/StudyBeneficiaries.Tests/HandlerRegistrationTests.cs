using API.SystemBuild;
using Application;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StudyBeneficiaries.Tests;

/// <summary>
/// كل معالج MediatR يجب أن تُحلّ اعتمادياته من حاوية الخدمات الحقيقية للـ API:
/// صنف «Support» جديد لا يُسجَّل في AddApplicationRegistrationServices، أو واجهة بلا تنفيذ مسجّل في Infrastructure،
/// يفشل هنا بدل أن يفشل الطلب عند أول استدعاء في التشغيل.
/// </summary>
public sealed class HandlerRegistrationTests
{
    [Fact]
    public void Every_handler_dependency_is_registered()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\none;Database=none",
            ["JwtSettings:Key"] = new string('k', 64),
            ["JwtSettings:Issuer"] = "test",
            ["JwtSettings:Audience"] = "test"
        }).Build();
        var services = new ServiceCollection();
        services.AddAPIServices(config);
        var registered = services.Select(s => s.ServiceType).ToHashSet();

        var application = typeof(AddApplicationRegistrationServices).Assembly;
        var handlers = application.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(IsHandler)).ToList();
        Assert.NotEmpty(handlers);

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var checkedTypes = new HashSet<Type>();
        var queue = new Queue<(Type Owner, Type Type)>(handlers.Select(h => (h, h)));
        while (queue.Count > 0)
        {
            var (owner, type) = queue.Dequeue();
            if (!checkedTypes.Add(type)) continue;
            foreach (var p in type.GetConstructors().SelectMany(c => c.GetParameters()))
            {
                var needed = Unwrap(p.ParameterType);
                if (needed.Assembly != application) continue;   // أنواع الإطار (IMapper، ILogger…) خارج نطاق هذا الفحص
                if (!registered.Contains(needed)) missing.Add($"{owner.Name} ← {type.Name}({needed.Name})");
                else if (needed.IsClass) queue.Enqueue((owner, needed));   // صنف مسجّل من Application: اعتمادياته أيضاً
            }
        }
        Assert.True(missing.Count == 0, "اعتماديات غير مسجّلة:\n" + string.Join("\n", missing));
    }

    private static bool IsHandler(Type i) => i.IsGenericType &&
        (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>));

    private static Type Unwrap(Type t) =>
        t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? t.GetGenericArguments()[0] : t;
}
