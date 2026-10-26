using System.Globalization;
using System.Text;

namespace Application.Features.Attendance.Push
{
    public sealed record PushPunch(string Pin, DateTime Time, int Status, int Verify, string WorkCode);
    public sealed record PushUser(string Pin, string Name, int Privilege, string Card);
    public sealed record PushTemplate(string Pin, int Type, int No, int Index, bool Valid, bool Duress,
        string MajorVersion, string MinorVersion, int Format, string Template);
    public sealed record PushBioPhoto(string Pin, int Type, byte[] Content);
    public sealed record PushPhoto(string Pin, DateTime Time, byte[] Content);
    public sealed record PushOperations(List<PushUser> Users, List<PushTemplate> Templates, List<PushBioPhoto> BioPhotos);
    public sealed record PushCommandResult(long Id, int ReturnCode, string Command);

    /// <summary>ما يبلّغ به الجهاز عن نفسه (معلمة INFO في الاستطلاع، أو جدول options)</summary>
    public sealed record PushDeviceInfo(string? Model, string? Firmware, string? PushVersion, string? Ip,
        int? Users, int? Fingers, int? Faces, int? Punches, string? FingerAlgorithm, string? FaceAlgorithm)
    {
        public static readonly PushDeviceInfo Empty = new(null, null, null, null, null, null, null, null, null, null);
    }

    /// <summary>
    /// بروتوكول Push/ADMS لأجهزة البصمة (يُكتب من الصفر، بلا أي شيفرة من برامج الشركة): قراءة ما يرسله الجهاز وصياغة ما يُرسل إليه.
    /// النصوص أسطر، والحقول مفصولة بـ Tab بصيغة «مفتاح=قيمة» (إلا سطر البصمة: حقول بالترتيب).
    /// ما يختلف بين إصدارات البرنامج الثابت يُقرأ بتسامح: الحقل المجهول يُهمل، والسطر التالف يُتخطى.
    /// </summary>
    public static class PushProtocol
    {
        public const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
        /// <summary>أطول اسم يقبله الجهاز (يُقصّ الاسم الأطول)</summary>
        public const int MaxNameLength = 24;

        // ───────── ما يرسله الجهاز ─────────

        /// <summary>سطور البصمات (table=ATTLOG): PIN، الوقت، الحالة، طريقة التحقق، رمز العمل، …</summary>
        public static List<PushPunch> ParseAttLog(string body)
        {
            var punches = new List<PushPunch>();
            foreach (var line in Lines(body))
            {
                var f = line.Split('\t');
                if (f.Length < 2) continue;
                var pin = f[0].Trim();
                if (pin.Length == 0 || pin.Length > 20) continue;
                if (!DateTime.TryParseExact(f[1].Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) continue;
                punches.Add(new PushPunch(pin, time, Int(f, 2), Int(f, 3), f.Length > 4 ? f[4].Trim() : string.Empty));
            }
            return punches;
        }

        /// <summary>
        /// سجل العمليات (table=OPERLOG أو BIODATA): مستخدمون (USER)، قوالب (FP/FACE/BIODATA)، صور تسجيل (BIOPHOTO/USERPIC).
        /// أسطر العمليات الأخرى (OPLOG) تُهمل.
        /// </summary>
        public static PushOperations ParseOperations(string body)
        {
            var result = new PushOperations([], [], []);
            foreach (var line in Lines(body))
            {
                var space = line.IndexOf(' ');
                if (space <= 0) continue;
                var kind = line[..space].ToUpperInvariant();
                var v = KeyValues(line[(space + 1)..]);
                var pin = Get(v, "PIN");
                if (string.IsNullOrEmpty(pin) || pin.Length > 20) continue;
                switch (kind)
                {
                    case "USER":
                        result.Users.Add(new PushUser(pin, Get(v, "Name") ?? string.Empty, IntOf(Get(v, "Pri")), Get(v, "Card") ?? string.Empty));
                        break;
                    case "FP":       // قالب إصبع بالصيغة القديمة
                        if (Get(v, "TMP") is { Length: > 0 } fp)
                            result.Templates.Add(new PushTemplate(pin, 1, IntOf(Get(v, "FID")), 0, Get(v, "Valid") != "0", false, "10", "0", 0, fp));
                        break;
                    case "FACE":     // وجه بالأشعة تحت الحمراء بالصيغة القديمة
                        if (Get(v, "TMP") is { Length: > 0 } face)
                            result.Templates.Add(new PushTemplate(pin, 2, IntOf(Get(v, "FID")), 0, Get(v, "Valid") != "0", false, "", "", 0, face));
                        break;
                    case "BIODATA":
                        if (Get(v, "Tmp") is { Length: > 0 } tmp)
                            result.Templates.Add(new PushTemplate(pin, IntOf(Get(v, "Type")), IntOf(Get(v, "No")), IntOf(Get(v, "Index")),
                                Get(v, "Valid") != "0", Get(v, "Duress") == "1", Get(v, "MajorVer") ?? "", Get(v, "MinorVer") ?? "",
                                IntOf(Get(v, "Format")), tmp));
                        break;
                    case "BIOPHOTO":
                    case "USERPIC":
                        if (Get(v, "Content") is { Length: > 0 } content && TryBase64(content) is { Length: > 0 } image)
                            result.BioPhotos.Add(new PushBioPhoto(pin, kind == "USERPIC" ? 0 : IntOf(Get(v, "Type")), image));
                        break;
                }
            }
            return result;
        }

        /// <summary>
        /// معلمة INFO في الاستطلاع: «الإصدار,المستخدمون,الأصابع,السجلات,IP,خوارزمية الإصبع,خوارزمية الوجه,قوالب الوجه للشخص,الوجوه,…»
        /// </summary>
        public static PushDeviceInfo ParseInfo(string? info)
        {
            if (string.IsNullOrWhiteSpace(info)) return PushDeviceInfo.Empty;
            var f = info.Split(',');
            string? At(int i) => i < f.Length && f[i].Trim().Length > 0 ? f[i].Trim() : null;
            int? Num(int i) => int.TryParse(At(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
            var firmware = At(0);
            if (firmware?.StartsWith("Ver ", StringComparison.OrdinalIgnoreCase) == true) firmware = firmware[4..];
            return new PushDeviceInfo(null, firmware, null, At(4), Num(1), Num(2), Num(8), Num(3), At(5), At(6));
        }

        /// <summary>جدول options (معلومات الجهاز بعد المصافحة): «مفتاح=قيمة» مفصولة بفواصل، وقد يسبق المفتاح «~»</summary>
        public static PushDeviceInfo ParseOptions(string body)
        {
            var v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in body.Split([',', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                v[part[..eq].Trim().TrimStart('~')] = part[(eq + 1)..].Trim();
            }
            string? S(params string[] keys) => keys.Select(k => v.GetValueOrDefault(k)).FirstOrDefault(x => !string.IsNullOrEmpty(x));
            int? N(params string[] keys) => int.TryParse(S(keys), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
            return new PushDeviceInfo(S("DeviceName", "ProductName"), S("FirmVer", "FWVersion"), S("PushVersion", "PushProtVer"),
                S("IPAddress", "IP"), N("UserCount"), N("FPCount", "FingerFunOn_Count"), N("FaceCount"), N("TransactionCount", "AttLogCount"),
                S("FPVersion", "~ZKFPVersion", "ZKFPVersion"), S("FaceVersion", "ZKFaceVersion"));
        }

        /// <summary>نتائج الأوامر (devicecmd): سطر لكل أمر «ID=..&amp;Return=..&amp;CMD=..»</summary>
        public static List<PushCommandResult> ParseCommandResults(string body)
        {
            var results = new List<PushCommandResult>();
            foreach (var line in Lines(body))
            {
                long? id = null; int code = 0; var cmd = string.Empty;
                foreach (var part in line.Split('&'))
                {
                    var eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = part[..eq].Trim(); var value = part[(eq + 1)..].Trim();
                    if (key.Equals("ID", StringComparison.OrdinalIgnoreCase) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) id = n;
                    else if (key.Equals("Return", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out code);
                    else if (key.Equals("CMD", StringComparison.OrdinalIgnoreCase)) cmd = value;
                }
                if (id is long cid) results.Add(new PushCommandResult(cid, code, cmd));
            }
            return results;
        }

        /// <summary>
        /// صورة لحظة البصم (ATTPHOTO): ترويسة نصية («PIN=yyyyMMddHHmmss-PIN.jpg» و SN و size …) ثم بايت صفر ثم صورة JPEG.
        /// </summary>
        public static PushPhoto? ParsePunchPhoto(byte[] body)
        {
            var zero = Array.IndexOf(body, (byte)0);
            if (zero <= 0 || zero >= body.Length - 1) return null;
            var header = Encoding.ASCII.GetString(body, 0, zero);
            string? name = null;
            foreach (var line in Lines(header.Replace('&', '\n')))
            {
                var eq = line.IndexOf('=');
                if (eq > 0 && line[..eq].Trim().Equals("PIN", StringComparison.OrdinalIgnoreCase)) name = line[(eq + 1)..].Trim();
            }
            // «20261026080133-123.jpg»: الوقت ثم الرقم (صورة شخص غير معروف بلا رقم تُهمل)
            if (name == null) return null;
            var stem = Path.GetFileNameWithoutExtension(name);
            var dash = stem.IndexOf('-');
            if (dash != 14) return null;
            if (!DateTime.TryParseExact(stem[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return null;
            var pin = stem[(dash + 1)..];
            if (pin.Length is 0 or > 20) return null;
            var image = body[(zero + 1)..];
            return IsJpeg(image) ? new PushPhoto(pin, time, image) : null;
        }

        public static bool IsJpeg(byte[] data) => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

        // ───────── ما يُرسل إلى الجهاز ─────────

        /// <summary>رد المصافحة: مؤشرات آخر ما استلمناه وإعدادات الإرسال (الإرسال الفوري، فترة الاستطلاع، المنطقة الزمنية)</summary>
        public static string Handshake(string serialNumber, string attLogStamp, string operLogStamp, string photoStamp,
            int pollSeconds, int timeZoneHours, bool wantPhotos)
        {
            var flags = "TransData AttLog OpLog EnrollUser ChgUser EnrollFP ChgFP FACE UserPic BioPhoto" + (wantPhotos ? " AttPhoto" : string.Empty);
            return string.Join("\n",
                $"GET OPTION FROM: {serialNumber}",
                $"ATTLOGStamp={attLogStamp}",
                $"OPERLOGStamp={operLogStamp}",
                $"ATTPHOTOStamp={photoStamp}",
                "ErrorDelay=30",
                $"Delay={pollSeconds}",
                "TransTimes=00:00;14:05",
                "TransInterval=1",
                $"TransFlag={flags}",
                $"TimeZone={timeZoneHours}",
                "Realtime=1",
                "Encrypt=None",
                "ServerVer=2.4.1",
                "PushProtVer=2.4.1") + "\n";
        }

        /// <summary>رد الاستطلاع: أمر في كل سطر «C:رقم:النص»، أو OK إن لم يكن شيء</summary>
        public static string FormatCommands(IEnumerable<(long Id, string Text)> commands)
        {
            var lines = commands.Select(c => $"C:{c.Id}:{c.Text}").ToList();
            return lines.Count == 0 ? "OK" : string.Join("\n", lines) + "\n";
        }

        /// <summary>إضافة شخص أو تحديثه: Pri 14 = مدير على الجهاز</summary>
        public static string UpdateUser(string pin, string name, bool deviceAdmin, string? card) =>
            $"DATA UPDATE USERINFO PIN={pin}\tName={CleanName(name)}\tPri={(deviceAdmin ? 14 : 0)}\tPasswd=\tCard={card ?? string.Empty}\tGrp=1\tTZ=0000000100000000\tVerify=-1";

        public static string DeleteUser(string pin) => $"DATA DELETE USERINFO PIN={pin}";

        public static string UpdateTemplate(string pin, int type, int no, int index, bool valid, bool duress,
            string majorVersion, string minorVersion, int format, string template) =>
            $"DATA UPDATE BIODATA Pin={pin}\tNo={no}\tIndex={index}\tValid={(valid ? 1 : 0)}\tDuress={(duress ? 1 : 0)}\tType={type}" +
            $"\tMajorVer={majorVersion}\tMinorVer={minorVersion}\tFormat={format}\tTmp={template}";

        /// <summary>صورة تسجيل الوجه: الجهاز ذو الضوء المرئي يستخرج منها قالبه (لموديل بخوارزمية مختلفة)</summary>
        public static string UpdateBioPhoto(string pin, byte[] jpeg) =>
            $"DATA UPDATE BIOPHOTO PIN={pin}\tType=9\tSize={jpeg.Length}\tContent={Convert.ToBase64String(jpeg)}\tFormat=0\tUrl=\tPostBackTmpFlag=0";

        public static string QueryPunches(DateTime from, DateTime to) =>
            $"DATA QUERY ATTLOG StartTime={from.ToString(TimeFormat, CultureInfo.InvariantCulture)}\tEndTime={to.ToString(TimeFormat, CultureInfo.InvariantCulture)}";

        public static string QueryTemplates(int type) => $"DATA QUERY BIODATA Type={type}";
        public const string QueryUsers = "DATA QUERY USERINFO";
        public const string Info = "INFO";
        public const string Reboot = "REBOOT";

        public static string SetTime(DateTime local) => $"SET OPTION DateTime={EncodeTime(local)}";

        /// <summary>ترميز الوقت في خيارات الجهاز: ثوانٍ من 2000 بأشهر من 31 يوماً</summary>
        public static long EncodeTime(DateTime t) =>
            (((t.Year - 2000) * 12L + (t.Month - 1)) * 31 + (t.Day - 1)) * 86400 + t.Hour * 3600 + t.Minute * 60 + t.Second;

        /// <summary>الاسم بلا محارف تكسر السطر أو الحقل، ومقصوص إلى ما يقبله الجهاز</summary>
        public static string CleanName(string name)
        {
            var clean = new string(name.Where(c => c is not ('\t' or '\r' or '\n' or '=')).ToArray()).Trim();
            return clean.Length <= MaxNameLength ? clean : clean[..MaxNameLength];
        }

        // ───────── مساعدات ─────────

        private static IEnumerable<string> Lines(string body) =>
            body.Split('\n').Select(l => l.TrimEnd('\r', '\0')).Where(l => l.Trim().Length > 0);

        private static Dictionary<string, string> KeyValues(string text)
        {
            var v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in text.Split('\t'))
            {
                var eq = part.IndexOf('=');   // القيمة قد تحوي «=» (Base64)
                if (eq > 0) v[part[..eq].Trim()] = part[(eq + 1)..].Trim();
            }
            return v;
        }

        private static string? Get(Dictionary<string, string> v, string key) => v.TryGetValue(key, out var s) ? s : null;
        private static int IntOf(string? s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
        private static int Int(string[] f, int i) => i < f.Length ? IntOf(f[i].Trim()) : 0;

        private static byte[]? TryBase64(string s)
        {
            try { return Convert.FromBase64String(s); }
            catch (FormatException) { return null; }
        }
    }
}
