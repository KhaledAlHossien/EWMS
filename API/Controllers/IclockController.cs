using System.Text;
using Application.Features.Attendance.Push;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API.Controllers
{
    /// <summary>
    /// أجهزة البصمة (Push/ADMS): مسارات /iclock/* بنص عادي، بلا تسجيل دخول (الجهاز يعرّف نفسه برقمه التسلسلي).
    /// لا تُقبل إلا عبر بوابة الأجهزة: Nginx على المنفذ 8081 (يضيف X-EWMS-Device-Gateway)، أو منفذ Kestrel المباشر في التطوير
    /// (Attendance:DevicePort)، وتعمل فقط مع Attendance:Enabled. الأخطاء تُسجَّل ويُرد بنص قصير فيعيد الجهاز المحاولة.
    /// </summary>
    [ApiController]
    [Route("iclock")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [DeviceGateway]
    public class IclockController : ControllerBase
    {
        /// <summary>أكبر طلب من جهاز (دفعات القوالب وصور البصم)</summary>
        public const long MaxBody = 20 * 1024 * 1024;

        private readonly AttendancePushHandler _push;
        private readonly IAttendanceSettings _settings;
        private readonly ILogger<IclockController> _logger;

        public IclockController(AttendancePushHandler push, IAttendanceSettings settings, ILogger<IclockController> logger)
        {
            _push = push;
            _settings = settings;
            _logger = logger;
        }

        private string? Ip => HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();

        [HttpGet("cdata")]
        public Task<IActionResult> Handshake([FromQuery(Name = "SN")] string? sn, CancellationToken ct) =>
            RunAsync(null, () => _push.HandshakeAsync(sn, Ip, ct));

        [HttpPost("cdata")]
        [HttpPost("fdata")]
        [RequestSizeLimit(MaxBody)]
        public async Task<IActionResult> Upload([FromQuery(Name = "SN")] string? sn, [FromQuery] string? table,
            [FromQuery(Name = "Stamp")] string? stamp, CancellationToken ct)
        {
            var body = await ReadBodyAsync(ct);
            return await RunAsync(body, () => _push.UploadAsync(sn, Ip, table, stamp, body, ct));
        }

        [HttpPost("querydata")]
        [RequestSizeLimit(MaxBody)]
        public async Task<IActionResult> QueryData([FromQuery(Name = "SN")] string? sn, [FromQuery] string? tablename, CancellationToken ct)
        {
            var body = await ReadBodyAsync(ct);
            return await RunAsync(body, () => _push.QueryDataAsync(sn, Ip, tablename, body, ct));
        }

        [HttpGet("getrequest")]
        public Task<IActionResult> Poll([FromQuery(Name = "SN")] string? sn, [FromQuery(Name = "INFO")] string? info, CancellationToken ct) =>
            RunAsync(null, () => _push.PollAsync(sn, Ip, info, ct));

        [HttpPost("devicecmd")]
        public async Task<IActionResult> CommandResults([FromQuery(Name = "SN")] string? sn, CancellationToken ct)
        {
            var body = await ReadBodyAsync(ct);
            return await RunAsync(body, () => _push.CommandResultsAsync(sn, Ip, body, ct));
        }

        [HttpGet("rtdata")]
        public Task<IActionResult> RealTime() => RunAsync(null, () => Task.FromResult(_push.ServerTime()));

        [HttpGet("ping")]
        public Task<IActionResult> Ping() => RunAsync(null, () => Task.FromResult(PushReply.Ok()));

        private async Task<byte[]> ReadBodyAsync(CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }

        private async Task<IActionResult> RunAsync(byte[]? body, Func<Task<PushReply>> handle)
        {
            PushReply reply;
            try
            {
                reply = await handle();
            }
            catch (OperationCanceledException)
            {
                reply = new PushReply(499, "ERROR");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "تعذّر معالجة طلب جهاز البصمة {Path}{Query}", Request.Path, Request.QueryString);
                reply = new PushReply(500, "ERROR");
            }
            await RawLog.WriteAsync(_settings.RawLogFolder, Request, body, reply, _logger);
            return new ContentResult { StatusCode = reply.Status, Content = reply.Body, ContentType = "text/plain; charset=utf-8" };
        }
    }

    /// <summary>
    /// بوابة الأجهزة: الوحدة مفعّلة، والطلب وصل عبر Nginx الأجهزة (الترويسة) أو منفذ الأجهزة المباشر في التطوير.
    /// غير ذلك = 404 كأن المسار غير موجود.
    /// </summary>
    public sealed class DeviceGatewayAttribute : Attribute, IResourceFilter
    {
        public const string Header = "X-EWMS-Device-Gateway";

        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var settings = context.HttpContext.RequestServices.GetRequiredService<IAttendanceSettings>();
            var http = context.HttpContext;
            var viaGateway = http.Request.Headers[Header] == "1";
            var viaDevicePort = settings.DevicePort > 0 && http.Connection.LocalPort == settings.DevicePort;
            if (!settings.Enabled || (settings.RequireGateway && !viaGateway && !viaDevicePort))
                context.Result = new NotFoundResult();
        }

        public void OnResourceExecuted(ResourceExecutedContext context) { }
    }

    /// <summary>
    /// سجل التشخيص (Attendance:RawLogFolder): كل طلب خام من الأجهزة ورده في ملف يومي — للتحقق من البروتوكول على موديل جديد.
    /// المحتوى الثنائي (الصور) لا يُكتب، والطويل يُقصّ.
    /// </summary>
    internal static class RawLog
    {
        private const int MaxBodyChars = 8000;
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task WriteAsync(string? folder, HttpRequest request, byte[]? body, PushReply reply, ILogger logger)
        {
            if (folder == null) return;
            try
            {
                var text = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ')
                    .Append(request.Method).Append(' ').Append(request.Path).Append(request.QueryString)
                    .Append(" from ").Append(request.HttpContext.Connection.RemoteIpAddress).AppendLine();
                if (body is { Length: > 0 })
                {
                    var binary = Array.IndexOf(body, (byte)0) >= 0;
                    var content = binary ? $"[{body.Length} بايت فيها محتوى ثنائي]" : Encoding.UTF8.GetString(body);
                    if (content.Length > MaxBodyChars) content = content[..MaxBodyChars] + $"\n…[قُصّ: {body.Length} بايت]";
                    text.AppendLine(content);
                }
                text.Append("=> ").Append(reply.Status).Append(' ').AppendLine(reply.Body.Length > 2000 ? reply.Body[..2000] + "…" : reply.Body).AppendLine();

                Directory.CreateDirectory(folder);
                await Gate.WaitAsync();
                try { await System.IO.File.AppendAllTextAsync(Path.Combine(folder, $"iclock-{DateTime.Now:yyyy-MM-dd}.log"), text.ToString()); }
                finally { Gate.Release(); }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "تعذّرت كتابة سجل تشخيص الأجهزة في {Folder}", folder);
            }
        }
    }
}
