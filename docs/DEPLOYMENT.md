# نشر EWMS على خادم Linux

> الخادم بلا إنترنت. الحزم تُبنى على جهاز التطوير وتُنقل يدوياً، **ولا تحتوي أي أسرار**:
> `powershell -ExecutionPolicy Bypass -File F:\EWMSSS\deploy\build-release.ps1` ← حزمتان (الطريقة المعتمدة، القسم A):
> `release\EWMS-<الإصدار>-api-linux-x64.tar.gz` للخادم و`release\EWMS-<الإصدار>-web.tar.gz` للواجهة (+ ملفا `.sha256`).
> مع `-Single` تُبنى حزمة واحدة مستقلة بذاتها لتثبيت جديد كلياً (الأقسام 0–4).

## A. تحديث الخادم الحالي (مجلدان) — الطريقة المعتمدة
الخادم الحالي: الـ API في `/var/www/dmc/EWMS-App` تشغّله الخدمة `ewms-api` (`dotnet API.dll`، يحتاج .NET 10 المثبّت)،
والواجهة في `/var/www/dmc/EWMS-App/angular` يقدّمها Nginx. **القاعدة: الخادم أولاً ثم الواجهة.**
حزمة الخادم **لا تحتوي `appsettings.json`**، فإعدادات الخادم ومفاتيحه تبقى كما هي (`appsettings.template.json` فيها مرجع للإعدادات فقط).

```bash
# 0) ارفع الحزم الأربع إلى /tmp (ويبمين: Tools ← Upload and Download) ثم تحقق منها
cd /tmp && sha256sum -c EWMS-*-api-linux-x64.tar.gz.sha256 EWMS-*-web.tar.gz.sha256

# 1) نسخة احتياطية: القاعدة والمجلد (التراجع = إعادتهما)
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -Q "BACKUP DATABASE [EWMS] TO DISK=N'/var/opt/mssql/data/EWMS_before_update.bak' WITH COMPRESSION, CHECKSUM, INIT"
cp -a /var/www/dmc/EWMS-App /var/www/dmc/EWMS-App.bak-$(date +%F)

# 2) الخادم (الترحيلات تُطبَّق عند الإقلاع)
systemctl stop ewms-api
tar -xzf /tmp/EWMS-*-api-linux-x64.tar.gz -C /var/www/dmc/EWMS-App
chown -R appuser:appuser /var/www/dmc/EWMS-App
systemctl start ewms-api && sleep 20 && systemctl is-active ewms-api && curl -s http://127.0.0.1:5000/health; echo
journalctl -u ewms-api -n 30 --no-pager     # إن لم يقلع: السجل يذكر الإعداد الناقص بالعربية

# 3) الواجهة (بعد نجاح الخادم): تُستبدل ملفاتها كلها
find /var/www/dmc/EWMS-App/angular -mindepth 1 -delete
tar -xzf /tmp/EWMS-*-web.tar.gz -C /var/www/dmc/EWMS-App/angular
chown -R appuser:appuser /var/www/dmc/EWMS-App/angular
```
- حدّث الصفحة في المتصفح (Ctrl+F5) بعد تحديث الواجهة.
- **التراجع:** `systemctl stop ewms-api`، ثم أعد المجلد من `EWMS-App.bak-…` والقاعدة من النسخة الاحتياطية، ثم `systemctl start ewms-api`.
- إعدادات الخادم الإلزامية (يرفض الإقلاع بدونها): نص الاتصال، `JwtSettings:Key` (32 حرفاً على الأقل)، `DeviceInventory:PasswordKey` (32 بايت Base64) — في `appsettings.json` على الخادم أو متغيرات بيئة الخدمة.
- **الإصدار 1.2.0 — حد رفع الملفات في Nginx (مرة واحدة):** حقول «ملفات» في طبقات الخرائط تقبل ملفاً حتى سقف النظام
  `MapLayers:MaxFileMegabytes` (الافتراضي 100MB، يغيّره مدير النظام في `appsettings.json` على الخادم أو بمتغير البيئة
  `MapLayers__MaxFileMegabytes`). Nginx يرفض أي طلب أكبر من `client_max_body_size` (الافتراضي **1MB** إن لم يُكتب)،
  فاجعله أكبر من السقف بقليل في ملف الموقع الحالي:
  ```bash
  grep -n client_max_body_size /etc/nginx/sites-enabled/ewms      # إن لم يظهر شيء فالحد 1MB
  sudo nano /etc/nginx/sites-enabled/ewms                          # داخل server { … } أضف أو عدّل: client_max_body_size 105m;
  sudo nginx -t && sudo systemctl reload nginx
  ```
  الملفات تُحفظ في قاعدة البيانات، فتكبر النسخ الاحتياطية بحجمها — راقب مساحة `/var/opt/mssql`.
- **الإصدار 1.2.0 — مفاتيح جديدة للسيرفر (مرة واحدة، قبل تشغيل الخدمة في الخطوة 2):** مفتاحا التطوير (`JwtSettings:Key`
  و`DeviceInventory:PasswordKey`) منشوران في مستودع GitHub العام، فلا يُستعملان على السيرفر:
  ```bash
  openssl rand -base64 48     # ← JwtSettings:Key
  openssl rand -base64 32     # ← DeviceInventory:PasswordKey
  sudo nano /var/www/dmc/EWMS-App/appsettings.json      # ضع القيمتين الجديدتين مكان القديمتين
  ```
  - مفتاح JWT الجديد: كل المستخدمين يسجّلون الدخول من جديد مرة واحدة.
  - مفتاح الأجهزة الجديد: كلمات سر الأجهزة المحفوظة قبل التغيير (مشفّرة بالمفتاح القديم) لا تُقرأ بعده —
    قرار 2026-10-14: لا بيانات مهمة بعد، فلا أداة لإعادة التشفير؛ تُعاد كتابة كلمة سر التركيب من صفحة التركيبات عند الحاجة.
  - **احفظ نسخة من مفتاح الأجهزة الجديد خارج السيرفر**: ضياعه يُضيّع كل كلمات سر الأجهزة المحفوظة بعده.
- **الإصدار 1.2.0 — الحماية من تخمين كلمات المرور وترويسات الأمان (مرة واحدة):**
  - الدخول: 5 محاولات خاطئة متتالية تقفل الحساب 15 دقيقة (يُشعَر صاحبه، ويفكّه تعيين كلمة مرور جديدة من صفحة المستخدمين)،
    و20 محاولة في الدقيقة لكل جهاز. **عدّاد الجهاز يعتمد على عنوانه الحقيقي**، فيجب أن يمرّره Nginx إلى الـ API،
    وإلا بدت كل الأجهزة جهازاً واحداً (127.0.0.1) وتشاركت الـ 20 محاولة:
    ```bash
    grep -n "X-Forwarded-For" /etc/nginx/sites-enabled/ewms    # يجب أن يظهر في location الـ API (/api/) والإشعارات (/hubs/)
    # إن لم يظهر، أضف داخل كل منهما:  proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    ```
  - كلمات المرور الجديدة: 8 أحرف على الأقل، فيها حرف ورقم، ولا تساوي البريد. الكلمات الحالية تبقى صالحة، وكل موظف يغيّر كلمته من «ملفي الشخصي».
  - ترويسات الأمان: ردود الـ API تأتي بها من الخادم. ملفات الواجهة يقدّمها Nginx، فأضفها له من الملف الذي في حزمة الخادم:
    ```bash
    sudo mkdir -p /etc/nginx/snippets
    sudo cp /var/www/dmc/EWMS-App/deploy/ewms-security-headers.conf /etc/nginx/snippets/
    sudo nano /etc/nginx/sites-enabled/ewms
    #   داخل location الذي فيه root …/angular (أو alias) أضف سطراً:  include snippets/ewms-security-headers.conf;
    sudo nginx -t && sudo systemctl reload nginx
    curl -sI http://127.0.0.1/ | grep -i content-security-policy        # يجب أن يظهر السطر
    ```
    إن لم تفتح الواجهة بعدها أو ظهرت صفحة بيضاء: احذف سطر الـ include وأعد تحميل Nginx، وأرسل رسالة الكونسول (F12) للمطوّر.
- **الإصدار 1.1.0:** كل تركيب جهاز صار ملك قسم من أضافه وفرعه، وخريطة كل فرع تُبنى من تركيبات أقسامه. التركيبات القديمة تأخذ قسم من أضافها تلقائياً؛ وما أضافه مدير بلا فرع يظهر «بلا قسم» في صفحة التركيبات — افتحه بالتعديل واختر قسمه فيظهر على خريطة فرعه.

## B. تثبيت جديد منفصل (Ubuntu 24.04 + SQL Server على جهاز مستقل)
الحزمتان نفسهما (بلا `-Single`): `EWMS-<الإصدار>-api-linux-x64.tar.gz` (framework-dependent، يحتاج **ASP.NET Core Runtime 10**)
و`EWMS-<الإصدار>-web.tar.gz` (ملفات الواجهة الثابتة). ملفات الإعداد في المستودع: `deploy/linux/split/`
(`ewms-api.service`، `ewms-api.env.example`، `nginx-ewms.conf`) و`deploy/linux/ewms-security-headers.conf` (موجود أيضاً في حزمة الـ API تحت `deploy/`).

**على جهاز SQL Server:**
```sql
CREATE DATABASE EWMS;
GO
CREATE LOGIN ewms_app WITH PASSWORD = N'<كلمة مرور قوية>';
GO
USE EWMS;
CREATE USER ewms_app FOR LOGIN ewms_app;
ALTER ROLE db_owner ADD MEMBER ewms_app;   -- الترحيلات تعدّل البنية عند كل إقلاع بعد تحديث
GO
```
- اسمح بالمنفذ 1433 في جدار الجهاز **من خادم التطبيق فقط**.
- المرفقات محفوظة داخل القاعدة افتراضياً (انظر «المرفقات» أدناه): راقب حجمها مقابل حد إصدار Express، أو انقل الجديدة منها إلى مشاركة SMB.

**على خادم التطبيق:**
```bash
# 0) تحقق من الحزم
cd /tmp && sha256sum -c EWMS-*-api-linux-x64.tar.gz.sha256 EWMS-*-web.tar.gz.sha256

# 1) مستخدم الخدمة والمجلدات
sudo useradd --system --no-create-home --shell /usr/sbin/nologin ewms
sudo mkdir -p /opt/ewms/api /var/www/ewms/web /etc/ewms
sudo tar -xzf /tmp/EWMS-*-api-linux-x64.tar.gz -C /opt/ewms/api
sudo tar -xzf /tmp/EWMS-*-web.tar.gz -C /var/www/ewms/web
sudo chown -R ewms:ewms /opt/ewms/api
sudo chown -R root:www-data /var/www/ewms/web && sudo chmod -R a+rX /var/www/ewms/web

# 2) الإعدادات والأسرار (نص الاتصال بجهاز SQL، المفتاحان الجديدان، أول مدير)
sudo cp deploy/linux/split/ewms-api.env.example /etc/ewms/ewms-api.env
openssl rand -base64 48     # ← JwtSettings__Key
openssl rand -base64 32     # ← DeviceInventory__PasswordKey (احفظ نسخة خارج الخادم)
sudo nano /etc/ewms/ewms-api.env
sudo chown root:ewms /etc/ewms/ewms-api.env && sudo chmod 640 /etc/ewms/ewms-api.env

# 3) الخدمة (الترحيلات تُطبَّق عند أول إقلاع وتنشئ الجداول)
sudo cp deploy/linux/split/ewms-api.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now ewms-api
sleep 20 && curl -s http://127.0.0.1:5000/health; echo
journalctl -u ewms-api -n 50 --no-pager      # إن لم يقلع: السجل يذكر الإعداد الناقص بالعربية

# 4) Nginx
sudo cp /opt/ewms/api/deploy/ewms-security-headers.conf /etc/nginx/snippets/
sudo cp deploy/linux/split/nginx-ewms.conf /etc/nginx/sites-available/ewms
sudo nano /etc/nginx/sites-available/ewms     # server_name = العنوان الذي يكتبه الموظفون
sudo ln -s /etc/nginx/sites-available/ewms /etc/nginx/sites-enabled/ewms
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
curl -sI http://127.0.0.1/ | grep -iE "200|content-security-policy"
```
- **أول دخول:** بالبريد وكلمة المرور من `Seed__…`، ثم احذف السطرين من `/etc/ewms/ewms-api.env` و`sudo systemctl restart ewms-api`،
  وغيّر كلمة المرور من «ملفي الشخصي».
- **التحديث لاحقاً:** كالقسم A مع هذه المسارات: أوقف `ewms-api`، فكّ حزمة الـ API فوق `/opt/ewms/api` (لا تحمل إعدادات فلا تمسّ
  `/etc/ewms`)، شغّل، ثم استبدل محتوى `/var/www/ewms/web` كاملاً.
- **HTTPS:** موصى به (أزرار النسخ وإشعارات سطح المكتب لا تعمل في المتصفح على HTTP لغير localhost، والجلسات تمرّ مكشوفة في الشبكة) —
  الكتلة المعلّقة في `nginx-ewms.conf` + شهادة من جهة شهادات داخلية مثبّتة على أجهزة الموظفين.

### المرفقات (الإصدار 1.2.0)
افتراضياً كل الملفات في القاعدة: جدول بيانات وصفية + جدول للمحتوى (`varbinary(max)`) كي لا تُحمَّل الملفات مع القوائم.
أو المحتوى في مجلد على مشاركة SMB (القسم التالي)، والبيانات الوصفية في الجدول نفسه دائماً (العمود `StorageKey`).
النوع يُحدَّد من محتوى الملف (توقيعه) لا من امتداده، والملفات التنفيذية والسكربتات وHTML/SVG والمستندات ذات الماكرو مرفوضة دائماً.

| الميزة | الجداول | الأنواع | الحدود |
|---|---|---|---|
| مرفقات طلبات الإجازة | `VacationAttachments` + `VacationAttachmentContents` | PDF، JPG، PNG | 3 ملفات × 5 MB لكل طلب |
| مرفقات مهام لوحة المهام | `AssignedTaskAttachments` + `AssignedTaskAttachmentContents` | PDF، صور، Word، Excel، PowerPoint (بلا ماكرو) | 10 ملفات × 10 MB لكل مهمة |
| حقول «ملفات» في طبقات خرائط الفروع | `MapEntryFiles` + `MapEntryFileContents` | صور، PDF، Office/ODF، نصوص وCSV، CAD (DWG/DXF)، ملفات خرائط (KML/KMZ/GeoJSON/GPX/SHP)، أرشيف (ZIP/RAR/7Z)، صوت وفيديو | يحددها مسؤول الطبقة: حتى 20 ملفاً لكل حقل، والملف حتى `MapLayers__MaxFileMegabytes` (100 MB افتراضياً) |
| التواقيع الإلكترونية | `UserSignatures` (نص data-URL) | PNG | نحو 300 KB لكل نسخة توقيع (النسخ القديمة لا تُحذف لأن القرارات الموقّعة تشير إليها) |

- **النسخ الاحتياطي:** في وضع `Database` نسخة القاعدة تشمل كل الملفات؛ مع مجلد SMB انظر القسم التالي.
- **حجم القاعدة:** إصدار SQL Server Express محدود الحجم لكل قاعدة؛ تحقق من الحد في إصداركم ومن حجم القاعدة دورياً
  (`EXEC sp_spaceused`)، فطبقات الخرائط هي الأكبر عادة (ملفات حتى 100 MB).

### مجلد المرفقات على File Server عبر SMB (اختياري)
الافتراضي `FileStorage__Mode=Database`: المحتوى في القاعدة كما في الجدول أعلاه. مع `FileSystem` تُحفظ **المرفقات الجديدة** (الإجازات والمهام وملفات الطبقات)
في مجلد على الخادم — مشاركة SMB يركّبها النظام كمسار عادي؛ التطبيق لا يتعامل مع SMB نفسه. التواقيع تبقى في القاعدة دائماً.

**1) على File Server:** مشاركة (مثلاً `ewms-files`) وحساب مخصص للتطبيق، صلاحيته قراءة وكتابة وحذف على هذه المشاركة **وحدها**.
فحص مكافحة الفيروسات على المشاركة مسموح (الملف لا يُعدَّل بعد كتابته).

**2) على خادم التطبيق:**
```bash
sudo apt install -y cifs-utils
sudo mkdir -p /mnt/ewms-files /etc/ewms
sudo install -m 600 /dev/null /etc/ewms/smb-credentials
sudo nano /etc/ewms/smb-credentials        # ثلاثة أسطر: username=…  password=…  domain=… (domain إن وُجد)
# سطر في /etc/fstab (seal = تشفير الاتصال، _netdev,nofail = لا يتعطل الإقلاع إن غاب File Server):
# //FILE_SERVER/ewms-files  /mnt/ewms-files  cifs  credentials=/etc/ewms/smb-credentials,vers=3.1.1,seal,uid=ewms,gid=ewms,file_mode=0600,dir_mode=0700,_netdev,nofail  0  0
sudo nano /etc/fstab
sudo systemctl daemon-reload && sudo mount /mnt/ewms-files
sudo -u ewms touch /mnt/ewms-files/.t && sudo -u ewms rm /mnt/ewms-files/.t && echo writable
```

**3) الإعدادات:** `FileStorage__Mode=FileSystem` و`FileStorage__Root=/mnt/ewms-files` (في `/etc/ewms/ewms-api.env` للتثبيت المنفصل،
أو `"FileStorage": { "Mode": "FileSystem", "Root": "/mnt/ewms-files" }` في `appsettings.json` على الخادم الحالي)، وأضف إلى قسم `[Unit]`
في ملف الخدمة `RequiresMountsFor=/mnt/ewms-files` (لا تبدأ الخدمة قبل تركيب المشاركة)، ثم `sudo systemctl daemon-reload` وأعد التشغيل.
- إن لم يكن المجلد موجوداً أو لا يقبل الكتابة **يتوقف الإقلاع** برسالة عربية في `journalctl`، و`/health` يصبح Unhealthy إن انقطعت المشاركة لاحقاً.

**كيف يعمل:**
- اسم الملف على القرص عشوائي: `vacations|tasks|map-files/<سنة>/<شهر>/<معرّف>`؛ الاسم الأصلي في القاعدة فقط. لا يقدّم Nginx المجلد أبداً:
  كل تنزيل يمرّ عبر الـ API بالصلاحيات نفسها.
- **الملفات السابقة للتحويل تبقى في القاعدة** وتُقرأ منها كما هي (لا أداة لنقلها — قرار 2026-10-14). لا ترجع إلى `Database` بعد وجود ملفات
  في المجلد إلا مع إبقاء `FileStorage__Root` مضبوطاً (الملفات ذات المفتاح تُقرأ منه دائماً).
- ملف سجلٍ محذوف (حذف مهمة أو سجل طبقة، ملف معلّق انتهت مهلته…) أو ملف كُتب ثم فشل حفظ سجله: تحذفه خدمة الخلفية **بعد 24 ساعة على الأقل**
  (تفحص المجلد كل 6 ساعات). لا تضع ملفات أخرى في المجلد؛ ما لا يطابق شكل المفاتيح لا يُمسّ.

**النسخ الاحتياطي:** نسخة القاعدة **أولاً** ثم نسخة المجلد (rsync أو لقطة على File Server). الملفات لا تُعدَّل أبداً، فنسخة مجلد بعد نسخة القاعدة
تحوي كل ما تشير إليه. الاستعادة: القاعدة ثم المجلد. بعد استعادة قاعدة أقدم تصبح الملفات الأحدث منها بلا سجل فتحذفها خدمة الخلفية بعد 24 ساعة —
انسخ المجلد جانباً قبل الاستعادة إن احتجت إليها.

### الحضور والانصراف: أجهزة البصمة على المنفذ 8081 (معطّلة افتراضياً)
أجهزة البصمة (ZKTeco، طريقة Push/ADMS) تتصل بالخادم بنفسها على منفذ مستقل لا يخدم إلا `/iclock/`، وبلا تسجيل دخول:
الجهاز يعرّف نفسه برقمه التسلسلي، فالجهاز الجديد يظهر «بانتظار الموافقة» في صفحة «أجهزة البصمة» ولا تُقبل بياناته حتى يُقبل
وتُحدَّد منطقته. بدون `Attendance__Enabled=true` يعيد `/iclock` 404 ولا تعمل خدمة المزامنة.

**1) الإعدادات** (في `/etc/ewms/ewms-api.env`، أو القسم `"Attendance"` في `appsettings.json` على الخادم الحالي):
```bash
openssl rand -base64 32        # مفتاح تشفير قوالب الوجه والإصبع — احفظ نسخة منه خارج الخادم مع مفتاح كلمات سر الأجهزة
```
| الإعداد | الافتراضي | المعنى |
|---|---|---|
| `Attendance__Enabled` | `false` | تفعيل الوحدة (الأجهزة والخدمة الخلفية) |
| `Attendance__TemplateKey` | — | 32 بايت Base64، **إلزامي مع التفعيل** (يتوقف الإقلاع بدونه). ضياعه لا يوقف الأجهزة (كل جهاز يحتفظ بقوالبه) لكن يلزم سحب القوالب من الأجهزة من جديد |
| `Attendance__RequireGateway` | `true` | لا يُقبل `/iclock` إلا عبر Nginx الأجهزة (الترويسة `X-EWMS-Device-Gateway`) |
| `Attendance__OfflineMinutes` | `10` | جهاز بلا اتصال منذ هذه المدة يُعدّ منقطعاً ويُرسل إشعاره |
| `Attendance__PollSeconds` | `10` | فترة استطلاع الجهاز للخادم |
| `Attendance__CommandsPerPoll` | `50` | أقصى عدد أوامر في رد استطلاع واحد |
| `Attendance__PhotoRetentionDays` | `90` | مدة الاحتفاظ بصور لحظة البصم (البصمة نفسها لا تُحذف أبداً) |
| `Attendance__TimeZoneHours` | `3` | توقيت الأجهزة عن UTC |
| `Attendance__RawLogFolder` | فارغ | مجلد سجل تشخيص لكل طلب خام من الأجهزة — للتحقق من موديل جديد فقط، ثم يُفرَّغ |

**2) Nginx والجدار الناري:** الملفان `ewms-devices.conf` و`ewms-devices-allow.conf` (في `deploy/` بحزمة الـ API وفي حزمة إعدادات التثبيت المنفصل):
```bash
sudo cp ewms-devices.conf /etc/nginx/sites-available/ewms-devices
sudo cp ewms-devices-allow.conf /etc/nginx/snippets/          # اكتب فيه شبكات الفروع (allow …؛ deny all)
sudo ln -s /etc/nginx/sites-available/ewms-devices /etc/nginx/sites-enabled/ewms-devices
sudo nginx -t && sudo systemctl reload nginx
sudo ufw allow from 10.20.0.0/16 to any port 8081 proto tcp   # سطر لكل شبكة فرع
```
وفي إعداد Nginx للواجهة (المنفذ 80) الكتلة `location /iclock/ { return 404; }` كي لا يصل أحد إلى `/iclock` من منفذ الواجهة —
أضفها إلى إعداد الخادم الحالي إن لم تكن فيه.

**3) على الجهاز** (القائمة ← Comm/الاتصال ← Cloud Server Setting؛ الأسماء تختلف قليلاً بين الإصدارات):
وضع الخادم ADMS، «Enable Domain Name» معطّل، عنوان الخادم = IP خادم EWMS، المنفذ `8081`، HTTPS والوكيل (Proxy) معطّلان.
خلال دقيقة يظهر الجهاز «بانتظار الموافقة» ويصل إشعار لمن يملك قبول الأجهزة: اقبله وحدد اسمه ومنطقته، فيُحمَّل عليه أشخاص منطقته
(من لهم رقم بصمة) خلال دقيقة، وتُسحب السجلات المسجّلة عليه قبل قبوله.
- **نقل جهاز من BioTime:** الجهاز يرسل إلى خادم واحد فقط؛ بعد تغيير العنوان لا يصل شيء إلى BioTime. انقل الأجهزة واحداً واحداً.
- **لا يظهر الجهاز:** `sudo tail -f /var/log/nginx/ewms-devices.access.log` — لا سطر = الشبكة أو الجدار الناري أو `allow`؛
  403 من Nginx = الشبكة غير مسموحة؛ 404 = `Attendance__Enabled` غير مفعّل أو الطلب لم يمر عبر بوابة الأجهزة.

**النسخ الاحتياطي:** البصمات والقوالب في القاعدة (جداول `Attendance…`). صور لحظة البصم في القاعدة، أو في مجلد المرفقات
(`attendance-photos/…`) مع `FileStorage__Mode=FileSystem`.

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
3. غيّر كلمة مرور المدير من «ملفي الشخصي» ← «تغيير كلمة المرور» (8 أحرف على الأقل، فيها حرف ورقم، ولا تساوي البريد؛ وكذلك `Seed__AdminPassword`، وإلا لا يُنشأ المدير والسبب في السجل).
4. أنشئ الفروع والأقسام والأدوار والمستخدمين. خريطة كل فرع تُبنى تلقائياً من بياناته (التركيبات المسجّلة باسم أقسامه) — لا إعداد لذلك.

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
مع مجلد مرفقات على SMB: انسخ المجلد **بعد** نسخة القاعدة (انظر «مجلد المرفقات على File Server»). انقل النسخ إلى جهاز آخر، وجرّب الاستعادة دورياً. النسخة تحوي كلمات سر الأجهزة **مشفّرة** (لا تُقرأ دون المفتاح).

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
