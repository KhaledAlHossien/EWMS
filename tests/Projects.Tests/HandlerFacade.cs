using System.Reflection;
using MediatR;

namespace Projects.Tests;

/// <summary>
/// واجهة اختبار فوق المعالجات المقسّمة (معالج لكل حالة استخدام): تجد معالج الطلب بالانعكاس وتبنيه من السياق المشترك،
/// فتبقى أجسام الاختبارات كما هي (<c>f.Handler.Handle(new XCommand(...), default)</c>) دون أن تعرف أيّ معالج.
/// </summary>
internal sealed class HandlerFacade(params object[] dependencies)
{
    private static readonly Assembly ApplicationAssembly = typeof(Application.Common.AppPermissions).Assembly;

    public async Task<TResponse> Handle<TResponse>(IRequest<TResponse> request, CancellationToken ct)
    {
        var requestType = request.GetType();
        var contract = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handlerType = ApplicationAssembly.GetTypes().Single(t => t is { IsClass: true, IsAbstract: false } && contract.IsAssignableFrom(t));

        var constructor = handlerType.GetConstructors().Single();
        var args = constructor.GetParameters()
            .Select(p => dependencies.FirstOrDefault(d => p.ParameterType.IsInstanceOfType(d))
                ?? throw new InvalidOperationException($"لا تبعية من نوع {p.ParameterType.Name} لبناء {handlerType.Name}"))
            .ToArray();

        var handler = constructor.Invoke(args);
        try
        {
            return await (Task<TResponse>)contract.GetMethod("Handle")!.Invoke(handler, [request, ct])!;
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
