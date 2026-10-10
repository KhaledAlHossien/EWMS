# نشر EWMS على خادم Linux

> الخادم بلا إنترنت: الحزمة فيها كل شيء — الخادم مستقل بذاته (لا يحتاج تثبيت .NET)، والواجهة والخطوط والخرائط داخلها.
> الحزمة تُبنى على جهاز التطوير: `powershell -ExecutionPolicy Bypass -File F:\EWMSSS\deploy\build-release.ps1`
> ← `release\EWMS-<الإصدار>-linux-x64.tar.gz` (+ ملف `.sha256`). **لا تحتوي أي أسرار.**

## 0. المتطلبات على الخادم
- Linux x64 (Ubuntu 22.04+/Debian 12/RHEL 9 أو ما يماثلها) مع **libicu** (موجودة عادة؛ للتحقق: `ldconfig -p | grep libicu`).
- **SQL Server 2019+** (على الخادم نفسه أو جهاز آخر في الشبكة) — النظام يستعمل ميزات SQL Server (rowversion، sp_getapplock).
- **Nginx** أمام التطبيق (الحزم تُنقل دون إنترنت إن لزم).
- ضبط ساعة الخادم (NTP داخلي إن وُجد). المنطقة الزمنية تُضبط للخدمة نفسها (`TZ=Asia/Damascus` في ملف الخدمة).

## 1. التثبيت لأول مرة
```bash
# 1) التحقق من سلامة الحزمة بعد نقلها
sha256sum -c EWMS-1.0.0-linux-x64.tar.gz.sha256

# 2) مستخدم نظام للخدمة (بلا دخول)
sudo useradd --system --no-create-home --shell /usr/sbin/nologin ewms

# 3) فك الحزمة في مجلد بإصدارها، ورابط ثابت app يشير إليه (التحديث والتراجع = تبديل الرابط)
sudo mkdir -p /opt/ewms/releases
sudo tar -xzf EWMS-1.0.0-linux-x64.tar.gz -C /opt/ewms/releases
sudo ln -sfn /opt/ewms/releases/EWMS-1.0.0-linux-x64/app /opt/ewms/app
sudo chmod +x /opt/ewms/app/API
sudo chown -R ewms:ewms /opt/ewms/releases
```

### 1.1 قاعدة البيانات
قاعدة جديدة فارغة؛ الجداول تُنشأ من الترحيلات عند أول إقلاع. في SQL Server (بحساب مدير):
```sql
CREATE DATABASE EWMS;
GO
CREATE LOGIN ewms_app WITH PASSWORD = N'<كلمة مرور قوية>';
GO
USE EWMS;
CREATE USER ewms_app FOR LOGIN ewms_app;
ALTER ROLE db_owner ADD MEMBER ewms_app;   -- الترحيلات تعدّل البنية
GO
```

### 1.2 الإعدادات والأسرار
```bash
sudo mkdir -p /etc/ewms
sudo cp /opt/ewms/releases/EWMS-1.0.0-linux-x64/deploy/ewms.env.example /etc/ewms/ewms.env
openssl rand -base64 48     # ← JwtSettings__Key
openssl rand -base64 32     # ← DeviceInventory__PasswordKey
sudo nano /etc/ewms/ewms.env          # نص الاتصال، المفتاحان، بريد وكلمة مرور أول مدير
sudo chown root:root /etc/ewms/ewms.env && sudo chmod 600 /etc/ewms/ewms.env
```

| المتغير | إلزامي | الوصف |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | ✔ | نص الاتصال بـ SQL Server (مستخدم `ewms_app`) |
| `JwtSettings__Key` | ✔ | مفتاح توقيع الجلسات، 32 حرفاً على الأقل |
| `DeviceInventory__PasswordKey` | ✔ | مفتاح تشفير كلمات سر الأجهزة: 32 بايت Base64 |
| `DeviceInventory__OwnerDepartmentId` | | قسم العمليات صاحب طبقة الأجهزة في الخريطة (بعد إنشاء الأقسام) |
| `Seed__AdminEmail`, `Seed__AdminPassword` | مرة واحدة | أول مدير نظام (انظر 1.5) |
| `Cors__AllowedOrigins__0`… | | فقط إن كانت الواجهة على عنوان آخر — هنا لا حاجة (نفس العنوان) |
| `Swagger__Enabled` | | `true` لعرض `/swagger` مؤقتاً للتشخيص (معطّل افتراضياً) |
| `Database__MigrateOnStartup` | | `true` افتراضياً (انظر §3) |

الخادم **يرفض الإقلاع** ويذكر في السجل كل إعداد ناقص أو غير صالح (قيمة `CHANGE_ME` لا تجتاز الفحص).

> **⚠ مفتاح تشفير كلمات سر الأجهزة:** ضياعه أو تغييره يُضيّع كل كلمات سر الأجهزة المحفوظة. احفظ نسخة منه خارج الخادم مع مسؤول النظام.
> مفتاح الجلسات إن تغيّر: يعيد المستخدمون تسجيل الدخول فقط.

### 1.3 الخدمة
```bash
sudo cp /opt/ewms/releases/EWMS-1.0.0-linux-x64/deploy/ewms.service /etc/systemd/system/ewms.service
sudo systemctl daemon-reload
sudo systemctl enable --now ewms
sudo journalctl -u ewms -f            # السجل المباشر (Ctrl+C للخروج)
curl -s http://127.0.0.1:5000/health  # ← Healthy
```

### 1.4 Nginx
```bash
sudo cp /opt/ewms/releases/EWMS-1.0.0-linux-x64/deploy/nginx-ewms.conf /etc/nginx/conf.d/ewms.conf
sudo nano /etc/nginx/conf.d/ewms.conf  # server_name، وشهادة HTTPS إن وُجدت
sudo nginx -t && sudo systemctl reload nginx
```
- الجدار الناري: افتح 80 (و443) فقط؛ المنفذ 5000 للجهاز نفسه.
- على RHEL مع SELinux: `sudo setsebool -P httpd_can_network_connect 1` كي يصل Nginx إلى التطبيق.

### 1.5 أول دخول
1. افتح `http://<الخادم>/` وسجّل الدخول ببريد وكلمة مرور أول مدير.
2. احذف سطري `Seed__AdminEmail` و`Seed__AdminPassword` من `/etc/ewms/ewms.env`، ثم `sudo systemctl restart ewms`.
3. غيّر كلمة مرور المدير من صفحة المستخدمين.
4. أنشئ الفروع والأقسام والأدوار والمستخدمين؛ ثم اضبط `DeviceInventory__OwnerDepartmentId` على رقم قسم العمليات وأعد التشغيل.

### 1.6 تحقق سريع بعد النشر
- [ ] `/health` = Healthy، والصفحة الرئيسية تفتح.
- [ ] جرس الإشعارات يتحدّث فوراً (WebSocket عبر Nginx).
- [ ] رفع مرفق في مهمة أو إجازة، وطباعة طلب إجازة.
- [ ] تصدير Excel (التركيبات أو المهام) — يعتمد على الخطوط في الخادم؛ إن فشل ثبّت حزمة خطوط (مثل `fonts-dejavu-core`).
- [ ] التاريخ في نموذج الإجازة صحيح (المنطقة الزمنية).

## 2. السجلات والمراقبة
- السجل: `sudo journalctl -u ewms --since today` (التحذيرات والأخطاء، وتفاصيل أخطاء 500 التي لا تظهر للمستخدم).
- المراقبة: `GET /health` → `200 Healthy` حين يتصل بقاعدة البيانات، و`503 Unhealthy` غير ذلك (بلا تفاصيل داخلية).
- الخدمة تُعاد تلقائياً إن توقفت (`Restart=always`).

## 3. النسخ الاحتياطي والتحديث
**نسخة يومية** (مثال cron بمستخدم يملك صلاحية النسخ؛ `sqlcmd` من حزمة mssql-tools):
```bash
# /etc/cron.d/ewms-backup — كل يوم 02:30، ويحتفظ بآخر 14 نسخة
30 2 * * * mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U backup_user -P '<...>' -C -Q "BACKUP DATABASE [EWMS] TO DISK=N'/var/opt/mssql/backup/EWMS_$(date +\%F).bak' WITH COMPRESSION, CHECKSUM, INIT" && find /var/opt/mssql/backup -name 'EWMS_*.bak' -mtime +14 -delete
```
انقل النسخ إلى جهاز آخر، وجرّب الاستعادة دورياً. النسخة تحوي كلمات سر الأجهزة **مشفّرة** (لا تُقرأ دون المفتاح).

**تحديث النسخة** (الترحيلات تعدّل البنية عند الإقلاع — النسخة الاحتياطية أولاً):
```bash
sqlcmd ... -Q "BACKUP DATABASE [EWMS] TO DISK=N'/var/opt/mssql/backup/EWMS_before_<الإصدار>.bak' WITH COMPRESSION, CHECKSUM, INIT"
sudo tar -xzf EWMS-<الإصدار>-linux-x64.tar.gz -C /opt/ewms/releases
sudo chmod +x /opt/ewms/releases/EWMS-<الإصدار>-linux-x64/app/API && sudo chown -R ewms:ewms /opt/ewms/releases
sudo ln -sfn /opt/ewms/releases/EWMS-<الإصدار>-linux-x64/app /opt/ewms/app
sudo systemctl restart ewms && curl -s http://127.0.0.1:5000/health
```
**التراجع:** أعد الرابط إلى المجلد السابق وأعد التشغيل — وإن كان الإصدار الجديد قد طبّق ترحيلات، فاستعد النسخة الاحتياطية أولاً.
`Database__MigrateOnStartup=false` لتطبيق الترحيلات يدوياً بسكربت (`dotnet ef migrations script --idempotent -p Infrastructure -s API -o migrate.sql` على جهاز التطوير).

## 4. قائمة التحقق قبل التشغيل الأول
- [ ] الحزمة سليمة (`sha256sum -c`).
- [ ] مفاتيح جديدة مولّدة ومحفوظة خارج الخادم (خاصة مفتاح تشفير الأجهزة)، و`ewms.env` بصلاحيات 600.
- [ ] مستخدم SQL مخصص (لا sa)، ونسخة احتياطية يومية مجرَّبة.
- [ ] Nginx + جدار ناري (80/443 فقط) + HTTPS إن أمكن.
- [ ] أول مدير أُنشئ، و`Seed__AdminPassword` حُذف، وكلمة المرور غُيّرت.
- [ ] `/health` يُراقَب.

> مفاتيح التطوير موجودة في تاريخ git؛ لا تُستعمل في الإنتاج. وتحذير للتطوير: `dotnet ef database update` يستهدف دائماً قاعدة `EWMS` المحلية (`DataContextFactory`).
