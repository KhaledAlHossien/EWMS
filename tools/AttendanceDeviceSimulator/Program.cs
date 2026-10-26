// محاكي أجهزة بصمة يتكلم بروتوكول Push/ADMS مع خادم EWMS: مصافحة، استطلاع وتنفيذ الأوامر، وإرسال بصمات.
// الاستعمال (من مجلد المشروع):
//   dotnet run --project tools/AttendanceDeviceSimulator -- --server http://localhost:8081 --devices 3 --pins 1001-1020 --minutes 2
// الخيارات:
//   --server   عنوان منفذ الأجهزة (الافتراضي http://localhost:8081)
//   --devices  عدد الأجهزة المحاكاة (الافتراضي 1) — الأرقام التسلسلية SIM0001، SIM0002، …
//   --prefix   بادئة الأرقام التسلسلية (الافتراضي SIM)
//   --pins     أرقام البصمة التي تبصم (مدى 1001-1020 أو قائمة 1,2,3)؛ الافتراضي: من وضعه الخادم على الجهاز
//   --rate     بصمات في الدقيقة لكل جهاز (الافتراضي 6)
//   --poll     فترة الاستطلاع بالثواني (الافتراضي من رد المصافحة)
//   --minutes  مدة التشغيل (الافتراضي 1)
//   --gateway  أضف ترويسة بوابة الأجهزة (للاختبار عبر منفذ الـ API نفسه حين RequireGateway=false لا يلزم)
// الأجهزة الجديدة تظهر «بانتظار الموافقة» في صفحة أجهزة البصمة؛ اقبلها لتُقبل بصماتها.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

var options = Options.Parse(args);
using var http = new HttpClient { BaseAddress = new Uri(options.Server.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
if (options.Gateway) http.DefaultRequestHeaders.Add("X-EWMS-Device-Gateway", "1");

var stats = new Stats();
using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(options.Minutes));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
Console.WriteLine($"المحاكي: {options.Devices} جهاز → {options.Server} لمدة {options.Minutes} دقيقة");

var devices = Enumerable.Range(1, options.Devices)
    .Select(i => new SimDevice($"{options.Prefix}{i:D4}", http, options, stats))
    .Select(d => d.RunAsync(stop.Token)).ToArray();
await Task.WhenAll(devices);

Console.WriteLine();
Console.WriteLine($"طلبات: {stats.Requests}  أخطاء: {stats.Errors}  رفض (غير مقبول): {stats.Rejected}");
Console.WriteLine($"بصمات أُرسلت: {stats.Punches}  أوامر نُفّذت: {stats.Commands}");
if (stats.Requests > 0) Console.WriteLine($"متوسط زمن الرد: {stats.TotalMs / stats.Requests:F1} ms  الأقصى: {stats.MaxMs} ms");

sealed class Options
{
    public string Server = "http://localhost:8081";
    public int Devices = 1;
    public string Prefix = "SIM";
    public List<string> Pins = [];
    public double Rate = 6;
    public int? Poll;
    public double Minutes = 1;
    public bool Gateway;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"قيمة ناقصة بعد {args[i]}");
            switch (args[i])
            {
                case "--server": o.Server = Next(); break;
                case "--devices": o.Devices = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--prefix": o.Prefix = Next(); break;
                case "--pins": o.Pins = ParsePins(Next()); break;
                case "--rate": o.Rate = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--poll": o.Poll = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--minutes": o.Minutes = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--gateway": o.Gateway = true; break;
                default: throw new ArgumentException($"خيار غير معروف: {args[i]}");
            }
        }
        return o;
    }

    private static List<string> ParsePins(string text)
    {
        if (text.Contains('-'))
        {
            var parts = text.Split('-');
            var (from, to) = (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
            return Enumerable.Range(from, to - from + 1).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();
        }
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

sealed class Stats
{
    public long Requests, Errors, Rejected, Punches, Commands, MaxMs;
    public double TotalMs;
    private readonly object _gate = new();

    public void Record(long ms, bool ok, bool rejected)
    {
        lock (_gate)
        {
            Requests++; TotalMs += ms; MaxMs = Math.Max(MaxMs, ms);
            if (!ok && !rejected) Errors++;
            if (rejected) Rejected++;
        }
    }
}

sealed class SimDevice(string serial, HttpClient http, Options options, Stats stats)
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
    private readonly ConcurrentDictionary<string, string> _users = new();   // الرقم ← الاسم (ما وضعه الخادم)
    private readonly List<string> _log = [];                                 // سطور البصمات المحفوظة في «ذاكرة الجهاز»
    private readonly Random _random = new(serial.GetHashCode());
    private int _poll = 10;

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var (_, handshake) = await SendAsync(HttpMethod.Get, $"iclock/cdata?SN={serial}&options=all&pushver=2.4.1&language=69", null, ct);
            foreach (var line in handshake.Split('\n'))
                if (line.StartsWith("Delay=", StringComparison.Ordinal) && int.TryParse(line[6..], out var delay)) _poll = delay;
            if (options.Poll is int poll) _poll = poll;
            await SendAsync(HttpMethod.Post, $"iclock/cdata?SN={serial}&table=options",
                $"~DeviceName=EWMS-Simulator,FirmVer=SIM-1.0,UserCount=0,FaceVersion=58,FPVersion=10,IPAddress=127.0.0.1", ct);

            var nextPunch = DateTime.UtcNow;
            var nextPoll = DateTime.UtcNow;
            while (!ct.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                if (now >= nextPunch && options.Rate > 0)
                {
                    await PunchAsync(ct);
                    nextPunch = now.AddSeconds(60 / options.Rate * (0.5 + _random.NextDouble()));
                }
                if (now >= nextPoll)
                {
                    await PollAsync(ct);
                    nextPoll = now.AddSeconds(_poll);
                }
                await Task.Delay(250, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task PunchAsync(CancellationToken ct)
    {
        var pins = options.Pins.Count > 0 ? options.Pins : _users.Keys.ToList();
        if (pins.Count == 0) return;
        var pin = pins[_random.Next(pins.Count)];
        var verify = _random.Next(4) == 0 ? 1 : 15;   // وجه غالباً، وإصبع أحياناً
        var line = $"{pin}\t{DateTime.Now.ToString(TimeFormat, CultureInfo.InvariantCulture)}\t0\t{verify}\t0\t0\t0";
        lock (_log) _log.Add(line);
        var (status, _) = await SendAsync(HttpMethod.Post, $"iclock/cdata?SN={serial}&table=ATTLOG&Stamp={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", line + "\n", ct);
        if (status == 200) Interlocked.Increment(ref stats.Punches);
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var info = $"SIM-1.0,{_users.Count},0,{_log.Count},127.0.0.1,10,58,12,0,111";
        var (status, body) = await SendAsync(HttpMethod.Get, $"iclock/getrequest?SN={serial}&INFO={Uri.EscapeDataString(info)}", null, ct);
        if (status != 200) return;

        var results = new StringBuilder();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("C:", StringComparison.Ordinal)) continue;
            var second = line.IndexOf(':', 2);
            if (second < 0) continue;
            var id = line[2..second];
            var command = line[(second + 1)..];
            await ExecuteAsync(command, ct);
            results.Append($"ID={id}&Return=0&CMD={command.Split(' ')[0]}\n");
            Interlocked.Increment(ref stats.Commands);
        }
        if (results.Length > 0)
            await SendAsync(HttpMethod.Post, $"iclock/devicecmd?SN={serial}", results.ToString(), ct);
    }

    private async Task ExecuteAsync(string command, CancellationToken ct)
    {
        string? Field(string key) => command.Split('\t', ' ').Select(p => p.Split('=', 2)).FirstOrDefault(p => p.Length == 2 && p[0] == key)?[1];

        if (command.StartsWith("DATA UPDATE USERINFO", StringComparison.Ordinal) && Field("PIN") is { } pin)
            _users[pin] = Field("Name") ?? string.Empty;
        else if (command.StartsWith("DATA DELETE USERINFO", StringComparison.Ordinal) && Field("PIN") is { } removed)
            _users.TryRemove(removed, out _);
        else if (command.StartsWith("DATA QUERY ATTLOG", StringComparison.Ordinal))
        {
            string[] lines;
            lock (_log) lines = [.. _log];
            if (lines.Length > 0)
                await SendAsync(HttpMethod.Post, $"iclock/cdata?SN={serial}&table=ATTLOG", string.Join("\n", lines) + "\n", ct);
        }
        else if (command.StartsWith("DATA QUERY USERINFO", StringComparison.Ordinal) && !_users.IsEmpty)
            await SendAsync(HttpMethod.Post, $"iclock/cdata?SN={serial}&table=OPERLOG",
                string.Join("\n", _users.Select(u => $"USER PIN={u.Key}\tName={u.Value}\tPri=0\tPasswd=\tCard=\tGrp=1")) + "\n", ct);
        else if (command == "INFO")
            await SendAsync(HttpMethod.Post, $"iclock/cdata?SN={serial}&table=options",
                $"~DeviceName=EWMS-Simulator,FirmVer=SIM-1.0,UserCount={_users.Count},TransactionCount={_log.Count}", ct);
    }

    private async Task<(int Status, string Body)> SendAsync(HttpMethod method, string url, string? body, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            }
            using var response = await http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;
            stats.Record(watch.ElapsedMilliseconds, status == 200, status == 403);
            if (status != 200 && status != 403) Console.WriteLine($"[{serial}] {method} {url.Split('?')[0]} → {status} {text}");
            return (status, text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            stats.Record(watch.ElapsedMilliseconds, false, false);
            Console.WriteLine($"[{serial}] تعذّر الاتصال: {ex.Message}");
            return (0, string.Empty);
        }
    }
}
