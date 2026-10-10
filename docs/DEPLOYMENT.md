# نشر EWMS على خادم الإنتاج

> الخادم بلا إنترنت: كل ما يلزم (الخطوط، الخرائط، المكتبات) داخل النسخة نفسها.

## 1. الإعدادات (متغيرات البيئة)

`appsettings.json` لا يحتوي أسراراً. في التطوير تأتي من `appsettings.Development.json` (لا يُحمَّل إلا حين `ASPNETCORE_ENVIRONMENT=Development`)،
وفي الإنتاج من **متغيرات البيئة** (`__` بدل `:`). الخادم **يرفض الإقلاع** برسالة تذكر الناقص إن غاب أحد الإلزامية.

| المتغير | إلزامي | الوصف |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | — | `Production` (الافتراضي إن لم يُضبط) |
| `ConnectionStrings__DefaultConnection` | ✔ | نص الاتصال بـ SQL Server |
| `JwtSettings__Key` | ✔ | مفتاح توقيع الجلسات، 32 حرفاً على الأقل |
| `DeviceInventory__PasswordKey` | ✔ | مفتاح تشفير كلمات سر الأجهزة: 32 بايت بصيغة Base64 (انظر التحذير أدناه) |
| `DeviceInventory__OwnerDepartmentId` | | قسم العمليات الذي تتبع له طبقة الأجهزة في الخريطة (الافتراضي 1) |
| `Cors__AllowedOrigins__0`, `__1`… | | عناوين الواجهة إن كانت على عنوان مختلف عن الخادم. فارغة = نفس العنوان فقط |
| `Swagger__Enabled` | | `true` لعرض `/swagger` (معطّل افتراضياً في الإنتاج) |
| `Database__MigrateOnStartup` | | `true` افتراضياً: تُطبَّق الترحيلات عند الإقلاع (انظر §4) |
| `Seed__AdminEmail`, `Seed__AdminPassword` | مرة واحدة | لإنشاء أول مدير نظام (انظر §3) |

### توليد المفاتيح (PowerShell)
```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Fill($b); [Convert]::ToBase64String($b)   # JwtSettings__Key
$b = New-Object byte[] 32; [Security.Cryptography.RandomNumberGenerator]::Fill($b); [Convert]::ToBase64String($b)   # DeviceInventory__PasswordKey
```

> **مفاتيح جديدة للإنتاج.** مفاتيح التطوير موجودة في تاريخ git، فلا تُستعمل في الإنتاج.
>
> **⚠ مفتاح تشفير كلمات سر الأجهزة:** كلمات سر الأجهزة المحفوظة مشفّرة به؛ **ضياعه أو تغييره يُضيّعها كلها**.
> احفظ نسخة منه خارج الخادم (مع مسؤول النظام). وإن نُقلت قاعدة بيانات فيها كلمات سر مشفّرة، يُنقل معها المفتاح الذي شُفّرت به.
> تغيير مفتاح الجلسات لا يضرّ شيئاً سوى أن المستخدمين يسجّلون الدخول من جديد.

## 2. التشغيل
- نشر النسخة: `dotnet publish API -c Release -o <مجلد النشر>`، ثم الواجهة: `npm run build` في مجلد الواجهة.
- HTTPS: الخادم يحوّل HTTP إلى HTTPS (`UseHttpsRedirection`)، فيلزم شهادة على IIS أو على الوكيل العكسي.
- **فحص الصحة:** `GET /health` → `200 Healthy` حين يتصل بقاعدة البيانات، و`503 Unhealthy` غير ذلك (بلا تفاصيل داخلية). يُضاف إلى أداة المراقبة.
- **السجلات:** على Windows تُكتب التحذيرات والأخطاء في سجل الأحداث (Event Viewer ← Application، المصدر `.NET Runtime`/اسم التطبيق). على IIS يمكن تفعيل `stdoutLogEnabled` في `web.config` مؤقتاً لتشخيص مشكلة إقلاع.
- رسائل أخطاء 500 لا تُظهر تفاصيلها للمستخدم خارج Development؛ التفاصيل في السجل.

## 3. أول مدير نظام
لا كلمة مرور في الكود. عند الإقلاع، **إن لم يوجد أي مستخدم بدور SuperAdmin**، يُنشأ مدير من `Seed__AdminEmail` و`Seed__AdminPassword` (8 أحرف على الأقل):
1. اضبط المتغيرين، وشغّل الخادم مرة (يُكتب في السجل «أُنشئ مدير النظام الأول»).
2. **احذف `Seed__AdminPassword`** من الإعدادات، وأعد التشغيل.
3. سجّل الدخول وغيّر كلمة المرور من صفحة المستخدمين.

إن وُجد مدير مسبقاً فهذه الإعدادات تُتجاهل. وإن غابت ولا مدير، يُكتب تحذير في السجل ولا يُنشأ شيء.

## 4. قاعدة البيانات: النسخ الاحتياطي والتحديث
**قبل كل تحديث للنسخة** (الترحيلات تعدّل البنية عند الإقلاع):
```sql
BACKUP DATABASE [EWMS] TO DISK = N'D:\Backups\EWMS_before_update.bak' WITH COMPRESSION, CHECKSUM, INIT;
```
- **نسخة يومية** مجدولة (SQL Server Agent، أو Task Scheduler مع `sqlcmd`)، تُحفظ خارج قرص الخادم، وتُجرَّب استعادتها دورياً.
- ملف النسخة يحتوي كلمات سر الأجهزة **مشفّرة**؛ لا تُقرأ دون المفتاح (§1).
- `Database__MigrateOnStartup=false` إن أردت تطبيق الترحيلات يدوياً: `dotnet ef migrations script --idempotent -p Infrastructure -s API -o migrate.sql` ثم تشغيله على القاعدة قبل تشغيل النسخة الجديدة.
- تحذير التطوير: `dotnet ef database update` يستهدف دائماً قاعدة `EWMS` المحلية (`DataContextFactory`)، لا قاعدة الإنتاج.

## 5. قائمة التحقق قبل التشغيل الأول
- [ ] مفاتيح جديدة مولّدة ومحفوظة خارج الخادم (وخاصة مفتاح تشفير الأجهزة).
- [ ] المتغيرات الإلزامية مضبوطة، و`Swagger__Enabled` غير مضبوط (أو `false`).
- [ ] `Cors__AllowedOrigins` = عنوان الواجهة فقط إن كانت على عنوان مختلف.
- [ ] شهادة HTTPS.
- [ ] نسخة احتياطية مجدولة ومجرَّبة.
- [ ] `/health` يُراقَب.
- [ ] أول مدير أُنشئ، و`Seed__AdminPassword` حُذف، وكلمة مروره غُيّرت.
