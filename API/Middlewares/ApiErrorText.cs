using System.Text.RegularExpressions;

namespace API.Middlewares
{
    /// <summary>
    /// رسائل الخطأ للمستخدم عربية دائماً (قرار المستخدم 2026-11-09): أخطاء ربط الطلب في إطار العمل
    /// («The JSON value could not be converted to System.Int32…») تُترجم هنا إلى جملة عربية باسم الحقل،
    /// وأي استثناء تقني بلا عربية لا يُعرض نصّه للمستخدم (يبقى في السجل).
    /// </summary>
    public static class ApiErrorText
    {
        private static readonly Regex ArabicLetter = new(@"[ء-ي]", RegexOptions.Compiled);
        private static readonly Regex LastSegment = new(@"([A-Za-z_][A-Za-z0-9_]*)(\[\d+\])*$", RegexOptions.Compiled);

        public static bool HasArabic(string? text) => !string.IsNullOrEmpty(text) && ArabicLetter.IsMatch(text);

        /// <summary>أسماء الحقول الشائعة بالعربية (غير المذكور يظهر باسمه البرمجي)</summary>
        private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["buildingCount"] = "عدد المباني", ["buildingsArea"] = "المساحة الإجمالية للمباني", ["siteArea"] = "مساحة الموقع العام",
            ["initialCost"] = "التكلفة المبدئية", ["quantity"] = "الكمية", ["unitPrice"] = "سعر الوحدة", ["staffCount"] = "عدد الكوادر",
            ["requiredDays"] = "المدة اللازمة بالأيام", ["weight"] = "الوزن", ["progress"] = "نسبة الإنجاز", ["allocatedBudget"] = "الميزانية المخصصة",
            ["latitude"] = "خط العرض", ["longitude"] = "خط الطول", ["defaultOrder"] = "الترتيب", ["pageSize"] = "حجم الصفحة", ["page"] = "رقم الصفحة",
            ["requestDate"] = "تاريخ الطلب", ["responseDate"] = "تاريخ الرد", ["plannedStartDate"] = "بداية التخطيط", ["plannedEndDate"] = "نهاية التخطيط",
            ["studyStartDate"] = "بدء الدراسة", ["studyDeadline"] = "نهاية الدراسة", ["assignmentDate"] = "تاريخ التكليف", ["orderDate"] = "تاريخ الأمر",
            ["drawingDate"] = "تاريخ المخطط", ["versionDate"] = "تاريخ النسخة", ["cdaDate"] = "تاريخ الاعتماد", ["requestId"] = "رقم الطلب", ["assignmentId"] = "رقم التكليف",
            ["responseId"] = "رقم الرد", ["startOrderId"] = "رقم أمر البدء", ["projectId"] = "رقم المشروع", ["stageId"] = "رقم المرحلة", ["siteId"] = "الموقع",
            ["departmentId"] = "القسم", ["officeId"] = "المكتب", ["branchId"] = "الفرع", ["urgency"] = "الاستعجال", ["recommendation"] = "التوصية الفنية"
        };

        /// <summary>اسم الحقل من مسار الخطأ («$.buildings[0].buildingCount» ← «عدد المباني»)</summary>
        public static string Label(string key)
        {
            var match = LastSegment.Match(key ?? string.Empty);
            var name = match.Success ? match.Groups[1].Value : key ?? string.Empty;
            return Labels.TryGetValue(name, out var label) ? label : name;
        }

        /// <summary>جملة عربية لخطأ ربط من إطار العمل؛ الرسالة العربية (من FluentValidation مثلاً) تبقى كما هي</summary>
        public static string Translate(string key, string message)
        {
            if (HasArabic(message)) return message;
            // جذر المسار ($) = الجسم كله: JSON مكسور أو بنية خاطئة
            if (key is "$" or "" && !message.Contains("is required", StringComparison.OrdinalIgnoreCase)) return "البيانات المرسلة غير صالحة";
            var field = Label(key);

            if (message.Contains("could not be converted to", StringComparison.OrdinalIgnoreCase))
            {
                if (Regex.IsMatch(message, @"System\.(Int16|Int32|Int64|Byte|UInt)")) return $"قيمة غير صالحة في «{field}»: يلزم عدد صحيح بلا كسور";
                if (Regex.IsMatch(message, @"System\.(Decimal|Double|Single)")) return $"قيمة غير صالحة في «{field}»: يلزم رقم";
                if (message.Contains("System.DateTime") || message.Contains("System.DateOnly")) return $"قيمة غير صالحة في «{field}»: يلزم تاريخ صحيح";
                if (message.Contains("System.Boolean")) return $"قيمة غير صالحة في «{field}»: يلزم نعم أو لا";
                return $"قيمة غير صالحة في «{field}»";
            }
            if (message.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase)) return "البيانات المرسلة ناقصة";
            if (message.Contains("is required", StringComparison.OrdinalIgnoreCase))
                return key is "dto" or "request" or "command" or "body" or "" ? "البيانات المرسلة ناقصة" : $"الحقل «{field}» مطلوب";
            return $"قيمة غير صالحة في «{field}»";
        }

        /// <summary>رسالة استثناء للمستخدم: العربية كما هي، وغيرها (استثناء تقني) نص عام</summary>
        public static string ArabicOrGeneric(string message, string generic = "تعذّر تنفيذ الطلب: بيانات غير صالحة") =>
            HasArabic(message) ? message : generic;
    }
}
