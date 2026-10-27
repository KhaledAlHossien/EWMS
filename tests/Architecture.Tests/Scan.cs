using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Architecture.Tests;

/// <summary>مسح بالانعكاس للتجمّعات الأربعة: يُستعمل في كل قواعد المعمارية</summary>
internal static class Scan
{
    public static readonly Assembly Api = typeof(Program).Assembly;
    public static readonly Assembly Application = typeof(Application.Common.AppPermissions).Assembly;
    public static readonly Assembly Domain = typeof(Domain.Entities.User).Assembly;
    public static readonly Assembly Infrastructure = typeof(Infrastructure.Persistence.Data.DataContext).Assembly;

    public static IEnumerable<Type> Controllers() =>
        Api.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t)).OrderBy(t => t.Name);

    /// <summary>نقاط النهاية: كل دالة عامة عليها HttpGet/Post/Put/Delete</summary>
    public static IEnumerable<ActionInfo> Actions() =>
        Controllers().SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(m => new ActionInfo(c, m)));

    public sealed record ActionInfo(Type Controller, MethodInfo Method)
    {
        public string Name => $"{Controller.Name.Replace("Controller", string.Empty)}.{Method.Name}";

        public bool AllowsAnonymous => Method.GetCustomAttributes<AllowAnonymousAttribute>().Any();

        /// <summary>السياسة الفعلية: سياسة الدالة إن وُجدت وإلا سياسة المتحكم؛ null = بلا سياسة (مصادقة فقط أو لا شيء)</summary>
        public string? Policy =>
            Method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null)
            ?? Controller.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).FirstOrDefault(p => p != null);

        public bool Authenticated =>
            Method.GetCustomAttributes<AuthorizeAttribute>().Any() || Controller.GetCustomAttributes<AuthorizeAttribute>().Any();
    }

    public static string Lines(IEnumerable<string> items) => string.Join(Environment.NewLine, items.OrderBy(x => x).Select(x => "  - " + x));
}
